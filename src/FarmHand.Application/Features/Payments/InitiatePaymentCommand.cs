using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.ValueObjects;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Platform commission charged on top of the vet's fee.</summary>
public static class PaymentPolicy
{
    public const decimal PlatformCommissionPercentage = 5m;

    public static decimal Commission(decimal amount) =>
        Math.Round(amount * PlatformCommissionPercentage / 100m, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Creates a pending payment and obtains a provider reference.</summary>
/// <param name="VetRequestId">Accepted vet request being paid for; the caller must own its farm.</param>
/// <param name="Amount">Vet's fee; must equal the request's fee.</param>
/// <param name="Currency">ISO currency code; must equal the request's currency.</param>
/// <param name="PlatformCommissionPercentage">Fixed at 5%.</param>
/// <param name="IdempotencyKey">Optional client key; reused keys return the existing pending payment.</param>
public record InitiatePaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string Currency,
    decimal PlatformCommissionPercentage = PaymentPolicy.PlatformCommissionPercentage,
    string? IdempotencyKey = null) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(
    string TransactionReference,
    string? CheckoutUrl,
    decimal PlatformCommission,
    decimal TotalAmount,
    decimal VetPayoutAmount,
    bool VetSubaccountUsed,
    string IdempotencyKey,
    string? SubaccountId,
    string Mode = PaymentMode.Simulated,
    Guid PaymentId = default,
    string Status = PaymentStatus.Pending);

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public InitiatePaymentCommandHandler(
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        IVetRequestRepository vetRequestRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser,
        IAuditLogger audit)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _vetRequestRepository = vetRequestRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        var incident = await _unitOfWork.Incidents.GetByIdAsync(vetRequest.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");

        // Only the farm owner (or an admin) pays; vets never initiate charges.
        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        if (!_paymentService.IsEnabled)
            throw PublicException.Unavailable("Payments are not configured on this server.", "payments_disabled");

        if (vetRequest.FarmerCost.Amount <= 0)
            throw PublicException.Conflict("This request does not require payment.");

        if (vetRequest.Status == RequestStatus.Pending)
            throw PublicException.Conflict("A vet has not accepted this request yet. You will be able to pay once a vet accepts.");

        if (vetRequest.Status != RequestStatus.Responded)
            throw PublicException.Conflict("This request is closed and can no longer be paid.");

        var existingPayments = (await _paymentRepository.GetByVetRequestIdAsync(vetRequest.Id, ct)).ToList();
        if (existingPayments.Any(p => p.Status == PaymentStatus.Completed))
            throw PublicException.Conflict("This vet request has already been paid.");

        var currency = request.Currency.Trim().ToUpperInvariant();
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        var existingPayment = existingPayments.FirstOrDefault(p => p.IdempotencyKey == idempotencyKey);
        if (existingPayment is not null)
        {
            if (existingPayment.Amount.Amount != request.Amount
                || !string.Equals(existingPayment.Amount.Currency, currency, StringComparison.OrdinalIgnoreCase))
                throw PublicException.Conflict("This idempotency key was already used with different payment details.");

            if (existingPayment.Status != PaymentStatus.Pending)
                throw PublicException.Conflict("This idempotency key was already used for a payment that is no longer pending. Use a new key.");

            return ToResult(existingPayment);
        }

        // Resume an interrupted checkout instead of creating a duplicate pending payment.
        var pendingPayment = existingPayments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
        if (pendingPayment is not null)
        {
            if (pendingPayment.Amount.Amount == request.Amount
                && string.Equals(pendingPayment.Amount.Currency, currency, StringComparison.OrdinalIgnoreCase))
                return ToResult(pendingPayment);

            throw PublicException.Conflict("A different checkout is already pending for this request. Cancel it before starting a new one.");
        }

        // The request's fee is authoritative; reject amount/currency tampering.
        if (request.Amount != vetRequest.FarmerCost.Amount)
            throw PublicException.BadRequest($"Amount must match the vet request fee of {vetRequest.FarmerCost.Amount} {vetRequest.FarmerCost.Currency}.");

        if (!string.Equals(vetRequest.FarmerCost.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw PublicException.BadRequest($"Currency must match the vet request currency of {vetRequest.FarmerCost.Currency}.");

        var customerEmail = _currentUser.Email
            ?? throw PublicException.Forbidden("An authenticated customer email is required to initialize payment.");

        var commission = PaymentPolicy.Commission(request.Amount);
        var totalAmount = Math.Round(request.Amount + commission, 2, MidpointRounding.AwayFromZero);

        // Split the charge automatically when the assigned vet has a payout subaccount.
        PaymentSplit? split = null;
        if (vetRequest.VetPartnerId is { } vetPartnerId)
        {
            var vetPartner = await _unitOfWork.VetPartners.GetByIdAsync(vetPartnerId, ct);
            if (vetPartner is { IsActive: true, HasPayoutAccount: true } && !string.IsNullOrWhiteSpace(vetPartner.PaymentSubaccountId))
                split = new PaymentSplit(vetPartner.PaymentSubaccountId, commission / totalAmount);
        }

        var result = await _paymentService.InitiatePaymentAsync(request.VetRequestId, totalAmount, currency, customerEmail, null, split, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.TransactionReference))
            throw PublicException.Upstream(result.ErrorMessage ?? "Payment could not be initialized. Please try again.");

        var payment = new Payment(request.VetRequestId, new Money(request.Amount, currency), PaymentPolicy.PlatformCommissionPercentage, _paymentService.Mode, _currentUser.UserId);
        payment.MarkPending(result.TransactionReference, result.CheckoutUrl, idempotencyKey, split is not null, split?.SubaccountId);

        await _paymentRepository.AddAsync(payment, ct);
        await _audit.RecordAsync("payment.initiated", payment.Id, $"request={vetRequest.Id}; total={totalAmount:0.00} {currency}; mode={payment.Mode}", ct: ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (Observations.SubmitObservationCommandHandler.IsUniqueViolation(ex))
        {
            throw PublicException.Conflict("Another checkout for this request was started at the same time. Refresh and try again.");
        }

        return ToResult(payment);
    }

    internal static InitiatePaymentResult ToResult(Payment payment) =>
        new(
            payment.TransactionReference,
            payment.CheckoutUrl,
            payment.PlatformCommission.Amount,
            payment.TotalAmount.Amount,
            payment.VetPayoutAmount.Amount,
            payment.VetSubaccountUsed,
            payment.IdempotencyKey ?? string.Empty,
            payment.SubaccountId,
            payment.Mode,
            payment.Id,
            payment.Status);
}

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.IdempotencyKey).MaximumLength(128).When(x => x.IdempotencyKey is not null);
        RuleFor(x => x.PlatformCommissionPercentage)
            .Equal(PaymentPolicy.PlatformCommissionPercentage)
            .WithMessage("Platform commission is fixed at 5%.");
    }
}
