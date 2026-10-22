using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Verifies a pending payment reference and marks the payment completed.</summary>
/// <param name="VetRequestId">Vet request being paid for.</param>
/// <param name="Amount">Total amount paid (vet cost + platform commission).</param>
/// <param name="Currency">ISO currency code; defaults to NGN.</param>
/// <param name="TransactionReference">Provider reference created by the initiate endpoint.</param>
public record RecordPaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string TransactionReference,
    string Currency = "NGN") : IRequest;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentService _paymentService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public RecordPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _paymentRepository = paymentRepository;
        _paymentService = paymentService;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Unit> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        await _authorization.EnsureVetRequestAccessAsync(request.VetRequestId, ct);

        var payment = await _paymentRepository.GetByTransactionReferenceAsync(request.TransactionReference, ct)
            ?? throw new KeyNotFoundException("Unknown payment reference. Initiate the payment first.");

        if (payment.VetRequestId != request.VetRequestId)
            throw new ArgumentException("The payment reference belongs to a different vet request.");

        var currency = request.Currency.Trim().ToUpperInvariant();

        // Validate against TotalAmount (vet cost + platform commission)
        if (payment.TotalAmount.Amount != request.Amount || !string.Equals(payment.TotalAmount.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Amount or currency does not match the initiated payment.");

        if (payment.Status == "Completed")
            return Unit.Value;

        if (payment.Status != "Pending")
            throw new InvalidOperationException($"Payment in status {payment.Status} cannot be completed.");

        var verification = await _paymentService.VerifyPaymentAsync(request.TransactionReference, ct);
        if (!verification.Success)
            throw new InvalidOperationException(verification.ErrorMessage ?? "Payment verification failed.");

        if (verification.Amount is { } verifiedAmount && verifiedAmount != payment.TotalAmount.Amount)
            throw new InvalidOperationException("Verified amount does not match the initiated payment.");

        if (!string.IsNullOrWhiteSpace(verification.Currency)
            && !string.Equals(verification.Currency, payment.TotalAmount.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Verified currency does not match the initiated payment.");

        payment.MarkCompleted(request.TransactionReference);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).MaximumLength(10);
        RuleFor(x => x.TransactionReference).NotEmpty().MaximumLength(200);
    }
}
