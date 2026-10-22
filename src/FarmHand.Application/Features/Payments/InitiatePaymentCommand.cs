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
public record InitiatePaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string Currency,
    decimal PlatformCommissionPercentage = 5m) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(
    string TransactionReference,
    string? CheckoutUrl,
    decimal PlatformCommission,
    decimal TotalAmount);

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
            ?? throw new KeyNotFoundException($"Vet request {request.VetRequestId} not found.");

        var currency = request.Currency.Trim().ToUpperInvariant();

        // The vet request's farmer cost is authoritative; reject amount/currency tampering.
        if (request.Amount != vetRequest.FarmerCost.Amount)
            throw new ArgumentException(
                $"Amount does not match the vet request farmer cost ({vetRequest.FarmerCost.Amount} {vetRequest.FarmerCost.Currency}).");

        if (!string.Equals(vetRequest.FarmerCost.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Currency does not match the vet request currency ({vetRequest.FarmerCost.Currency}).");

        var customerEmail = _currentUser.Email
            ?? throw new InvalidOperationException("No authenticated customer email available to initialize payment.");

        // Platform commission is calculated on top of the vet's charge
        var commission = request.Amount * request.PlatformCommissionPercentage / 100m;
        var totalAmount = request.Amount + commission;

        var result = await _paymentService.InitiatePaymentAsync(
            request.VetRequestId,
            totalAmount,
            currency,
            customerEmail,
            null,
            ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.TransactionReference))
            throw new InvalidOperationException(result.ErrorMessage ?? "Payment initiation failed.");

        var payment = new Payment(request.VetRequestId, new Money(request.Amount, currency), request.PlatformCommissionPercentage);
        payment.MarkPending(result.TransactionReference);

        await _paymentRepository.AddAsync(payment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new InitiatePaymentResult(result.TransactionReference, result.CheckoutUrl, commission, totalAmount);
    }
}

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).MaximumLength(10);
        RuleFor(x => x.PlatformCommissionPercentage).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100);
    }
}
