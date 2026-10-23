using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.ValueObjects;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Creates a pending payment and obtains a provider reference.</summary>
/// <param name="VetRequestId">Vet request being paid for; must be accessible to the caller.</param>
/// <param name="Amount">Vet's charge amount; must be greater than zero.</param>
/// <param name="Currency">ISO currency code, for example "NGN".</param>
/// <param name="PlatformCommissionPercentage">Platform commission percentage, default 5%.</param>
/// <param name="IdempotencyKey">Optional client key; reused keys return the existing pending payment.</param>
public record InitiatePaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string Currency,
    decimal PlatformCommissionPercentage = 5m,
    string? IdempotencyKey = null) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(
    string TransactionReference,
    string? CheckoutUrl,
    decimal PlatformCommission,
    decimal TotalAmount,
    decimal VetPayoutAmount,
    bool VetSubaccountUsed,
    string IdempotencyKey,
    string? SubaccountId);

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public InitiatePaymentCommandHandler(
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        IVetRequestRepository vetRequestRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _vetRequestRepository = vetRequestRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        await _authorization.EnsureVetRequestAccessAsync(request.VetRequestId, ct);

        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");

        if (vetRequest.Status == FarmHand.Domain.Enums.RequestStatus.Completed)
            throw PublicException.Conflict("This vet request is already completed.");

        var existingPayments = await _paymentRepository.GetByVetRequestIdAsync(vetRequest.Id, ct);
        if (existingPayments.Any(p => p.Status == "Completed"))
            throw PublicException.Conflict("This vet request has already been paid.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        var existingPayment = existingPayments.FirstOrDefault(p => p.IdempotencyKey == idempotencyKey);
        if (existingPayment is not null)
        {
            if (existingPayment.Amount.Amount != request.Amount
                || !string.Equals(existingPayment.Amount.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw PublicException.Conflict(
                    "This idempotency key was already used with different payment details.");
            }

            if (existingPayment.Status != "Pending")
                throw PublicException.Conflict(
                    "This idempotency key was already used for a payment that is no longer pending. Use a new key.");

            return ToResult(existingPayment);
        }

        // Avoid creating duplicate pending payments for the same vet request.
        var pendingPayment = existingPayments.FirstOrDefault(p => p.Status == "Pending");
        if (pendingPayment is not null)
        {
            if (pendingPayment.Amount.Amount == request.Amount
                && string.Equals(pendingPayment.Amount.Currency, request.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return ToResult(pendingPayment);
            }

            throw PublicException.Conflict(
                "This vet request already has a pending payment with different amount or currency. Use a new idempotency key and cancel the existing payment first.");
        }

        var currency = request.Currency.Trim().ToUpperInvariant();

        // The vet request's farmer cost is authoritative; reject amount/currency tampering.
        if (request.Amount != vetRequest.FarmerCost.Amount)
            throw PublicException.BadRequest(
                $"Amount must match the vet request charge of {vetRequest.FarmerCost.Amount} {vetRequest.FarmerCost.Currency}.");

        if (!string.Equals(vetRequest.FarmerCost.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw PublicException.BadRequest(
                $"Currency must match the vet request currency of {vetRequest.FarmerCost.Currency}.");

        var customerEmail = _currentUser.Email
            ?? throw PublicException.Forbidden("An authenticated customer email is required to initialize payment.");

        // Platform commission is calculated on top of the vet's charge and stored to 2 decimals.
        var commission = Math.Round(request.Amount * request.PlatformCommissionPercentage / 100m, 2, MidpointRounding.AwayFromZero);
        var totalAmount = Math.Round(request.Amount + commission, 2, MidpointRounding.AwayFromZero);

        // When the vet already has a payout subaccount, split the charge automatically.
        // The vet receives their full charge; the platform keeps the commission.
        PaymentSplit? split = null;
        if (vetRequest.VetPartnerId is { } vetPartnerId)
        {
            var vetPartner = await _unitOfWork.VetPartners.GetByIdAsync(vetPartnerId, ct);
            if (vetPartner is { IsActive: true, HasPayoutAccount: true } && !string.IsNullOrWhiteSpace(vetPartner.PaymentSubaccountId))
            {
                // Flutterwave expects the platform's commission as a fraction of the total charge.
                var platformCommissionRate = commission / totalAmount;
                split = new PaymentSplit(vetPartner.PaymentSubaccountId, platformCommissionRate);
            }
        }

        var result = await _paymentService.InitiatePaymentAsync(
            request.VetRequestId,
            totalAmount,
            currency,
            customerEmail,
            null,
            split,
            ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.TransactionReference))
            throw PublicException.Upstream(result.ErrorMessage ?? "Payment could not be initialized. Please try again.");

        var payment = new Payment(request.VetRequestId, new Money(request.Amount, currency), request.PlatformCommissionPercentage);
        payment.MarkPending(result.TransactionReference, result.CheckoutUrl, idempotencyKey, split is not null, split?.SubaccountId);

        await _paymentRepository.AddAsync(payment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToResult(payment);
    }

    private static InitiatePaymentResult ToResult(Payment payment) =>
        new(
            payment.TransactionReference,
            payment.CheckoutUrl,
            payment.PlatformCommission.Amount,
            payment.TotalAmount.Amount,
            payment.VetPayoutAmount.Amount,
            payment.VetSubaccountUsed,
            payment.IdempotencyKey ?? string.Empty,
            payment.SubaccountId);
}

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).MaximumLength(10);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128).When(x => x.IdempotencyKey is not null);
        RuleFor(x => x.PlatformCommissionPercentage)
            .Equal(5m)
            .WithMessage("Platform commission is fixed at 5%.");
    }
}
