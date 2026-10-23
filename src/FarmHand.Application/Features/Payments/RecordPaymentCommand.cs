using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>
/// Applies a provider verification to a stored payment. This is the only place a payment becomes
/// Completed: the provider must report success and, in live mode, the provider-verified amount and
/// currency must equal what was initiated.
/// </summary>
public class PaymentSettlement
{
    private readonly IAuditLogger _audit;

    public PaymentSettlement(IAuditLogger audit)
    {
        _audit = audit;
    }

    public record Outcome(string Status, string Message);

    public async Task<Outcome> ApplyAsync(Payment payment, PaymentVerification verification, string channel, CancellationToken ct)
    {
        if (payment.Status == PaymentStatus.Completed)
            return new Outcome(payment.Status, "Payment already confirmed.");

        if (payment.Status != PaymentStatus.Pending)
            return new Outcome(payment.Status, $"This payment is {payment.Status.ToLowerInvariant()}.");

        switch (verification.Outcome)
        {
            case PaymentVerificationOutcome.Successful:
                if (!payment.IsSimulated)
                {
                    if (verification.Amount is not { } amount || amount != payment.TotalAmount.Amount)
                    {
                        await _audit.RecordAsync("payment.amount_mismatch", payment.Id, $"expected={payment.TotalAmount.Amount}; verified={verification.Amount}", "system", ct);
                        return new Outcome(payment.Status, "The verified amount does not match this checkout. It has been flagged for review.");
                    }

                    if (!string.Equals(verification.Currency, payment.TotalAmount.Currency, StringComparison.OrdinalIgnoreCase))
                    {
                        await _audit.RecordAsync("payment.currency_mismatch", payment.Id, $"expected={payment.TotalAmount.Currency}; verified={verification.Currency}", "system", ct);
                        return new Outcome(payment.Status, "The verified currency does not match this checkout. It has been flagged for review.");
                    }
                }

                payment.MarkCompleted(verification.ProviderTransactionId);
                await _audit.RecordAsync("payment.completed", payment.Id, $"mode={payment.Mode}; via={channel}", ct: ct);
                return new Outcome(payment.Status, payment.IsSimulated
                    ? "Simulated payment confirmed. No money was moved."
                    : "Payment verified with the provider.");

            case PaymentVerificationOutcome.Failed:
                payment.MarkFailed(verification.Message);
                await _audit.RecordAsync("payment.failed", payment.Id, $"via={channel}", ct: ct);
                return new Outcome(payment.Status, "The provider reported that this payment failed. You can start a new checkout.");

            case PaymentVerificationOutcome.Error:
                return new Outcome(payment.Status, "We could not reach the payment provider. Your payment is still pending; check again shortly.");

            default:
                return new Outcome(payment.Status, "The checkout has not been completed yet.");
        }
    }
}

/// <summary>Verifies a payment with the provider by its reference and records the result.</summary>
public record VerifyPaymentCommand(string TransactionReference) : IRequest<PaymentStatusDto>;

public record PaymentStatusDto(PaymentDto Payment, string Message);

public class VerifyPaymentCommandHandler : IRequestHandler<VerifyPaymentCommand, PaymentStatusDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IResourceAuthorizationService _authorization;
    private readonly PaymentSettlement _settlement;

    public VerifyPaymentCommandHandler(IUnitOfWork unitOfWork, IPaymentService paymentService, IResourceAuthorizationService authorization, PaymentSettlement settlement)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _authorization = authorization;
        _settlement = settlement;
    }

    public async Task<PaymentStatusDto> Handle(VerifyPaymentCommand request, CancellationToken ct)
    {
        var payment = await _unitOfWork.Payments.GetByTransactionReferenceAsync(request.TransactionReference.Trim(), ct)
            ?? throw PublicException.NotFound("Unknown payment reference.");
        await EnsureOwnerAsync(payment, _unitOfWork, _authorization, ct);

        var outcome = payment.Status == PaymentStatus.Pending
            ? await _settlement.ApplyAsync(payment, await _paymentService.VerifyByReferenceAsync(payment.TransactionReference, ct), "verify", ct)
            : new PaymentSettlement.Outcome(payment.Status, payment.Status == PaymentStatus.Completed ? "Payment already confirmed." : $"This payment is {payment.Status.ToLowerInvariant()}.");

        await _unitOfWork.SaveChangesAsync(ct);
        return new PaymentStatusDto(payment.ToDto(includePayout: false), outcome.Message);
    }

    internal static async Task EnsureOwnerAsync(Payment payment, IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, CancellationToken ct)
    {
        var vetRequest = await unitOfWork.VetRequests.GetByIdAsync(payment.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        var incident = await unitOfWork.Incidents.GetByIdAsync(vetRequest.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");
        await authorization.EnsureFarmAccessAsync(incident.FarmId, ct);
    }
}

public class VerifyPaymentCommandValidator : AbstractValidator<VerifyPaymentCommand>
{
    public VerifyPaymentCommandValidator()
    {
        RuleFor(x => x.TransactionReference).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Legacy endpoint: verifies a payment the client claims to have made. Amount and currency must match the initiated payment.</summary>
/// <param name="VetRequestId">Vet request being paid for.</param>
/// <param name="Amount">Total amount paid (vet fee + platform commission).</param>
/// <param name="TransactionReference">Reference returned by the initiate endpoint.</param>
/// <param name="Currency">ISO currency code; defaults to NGN.</param>
public record RecordPaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string TransactionReference,
    string Currency = "NGN") : IRequest<PaymentStatusDto>;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, PaymentStatusDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public RecordPaymentCommandHandler(IUnitOfWork unitOfWork, IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    public async Task<PaymentStatusDto> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var payment = await _unitOfWork.Payments.GetByTransactionReferenceAsync(request.TransactionReference, ct)
            ?? throw PublicException.NotFound("Unknown payment reference. Initiate the payment first.");

        if (payment.VetRequestId != request.VetRequestId)
            throw PublicException.BadRequest("The payment reference belongs to a different vet request.");

        var currency = request.Currency.Trim().ToUpperInvariant();
        if (payment.TotalAmount.Amount != request.Amount || !string.Equals(payment.TotalAmount.Currency, currency, StringComparison.OrdinalIgnoreCase))
            throw PublicException.BadRequest("Amount or currency does not match the initiated payment.");

        return await _mediator.Send(new VerifyPaymentCommand(request.TransactionReference), ct);
    }
}

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.TransactionReference).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Cancels a pending checkout so a new one can be started. Live payments are re-verified first.</summary>
public record CancelPaymentCommand(string TransactionReference) : IRequest<PaymentStatusDto>;

public class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand, PaymentStatusDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly IResourceAuthorizationService _authorization;
    private readonly PaymentSettlement _settlement;
    private readonly IAuditLogger _audit;

    public CancelPaymentCommandHandler(IUnitOfWork unitOfWork, IPaymentService paymentService, IResourceAuthorizationService authorization, PaymentSettlement settlement, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _authorization = authorization;
        _settlement = settlement;
        _audit = audit;
    }

    public async Task<PaymentStatusDto> Handle(CancelPaymentCommand request, CancellationToken ct)
    {
        var payment = await _unitOfWork.Payments.GetByTransactionReferenceAsync(request.TransactionReference.Trim(), ct)
            ?? throw PublicException.NotFound("Unknown payment reference.");
        await VerifyPaymentCommandHandler.EnsureOwnerAsync(payment, _unitOfWork, _authorization, ct);

        if (payment.Status != PaymentStatus.Pending)
            throw PublicException.Conflict($"This payment is already {payment.Status.ToLowerInvariant()}.");

        if (!payment.IsSimulated)
        {
            // Never cancel a checkout the farmer may already have paid.
            var verification = await _paymentService.VerifyByReferenceAsync(payment.TransactionReference, ct);
            if (verification.Outcome == PaymentVerificationOutcome.Successful)
            {
                var outcome = await _settlement.ApplyAsync(payment, verification, "cancel-check", ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return new PaymentStatusDto(payment.ToDto(includePayout: false), outcome.Message);
            }

            if (verification.Outcome == PaymentVerificationOutcome.Error)
                throw PublicException.Upstream("We could not confirm the checkout status with the provider. Try again shortly.");
        }

        payment.Cancel();
        await _audit.RecordAsync("payment.cancelled", payment.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return new PaymentStatusDto(payment.ToDto(includePayout: false), "Checkout cancelled. You can start a new one.");
    }
}

/// <summary>
/// Processes a Flutterwave webhook whose signature the API already verified. The event body is never
/// trusted for amounts: the transaction is re-verified with the provider by id. Duplicate deliveries are no-ops.
/// </summary>
public record ProcessPaymentWebhookCommand(string EventType, string? TransactionId, string? Reference, string? Status) : IRequest<string>;

public class ProcessPaymentWebhookCommandHandler : IRequestHandler<ProcessPaymentWebhookCommand, string>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly PaymentSettlement _settlement;

    public ProcessPaymentWebhookCommandHandler(IUnitOfWork unitOfWork, IPaymentService paymentService, PaymentSettlement settlement)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _settlement = settlement;
    }

    public async Task<string> Handle(ProcessPaymentWebhookCommand request, CancellationToken ct)
    {
        var isCharge = request.EventType is "charge.completed" or "payment.completed";
        if (!isCharge || string.IsNullOrWhiteSpace(request.Reference))
            return "ignored";

        var payment = await _unitOfWork.Payments.GetByTransactionReferenceAsync(request.Reference, ct);
        if (payment is null)
            return "unknown_reference";

        if (payment.Status != PaymentStatus.Pending)
            return "already_processed";

        var verification = !string.IsNullOrWhiteSpace(request.TransactionId)
            ? await _paymentService.VerifyByTransactionIdAsync(request.TransactionId, ct)
            : await _paymentService.VerifyByReferenceAsync(request.Reference, ct);

        // The provider must confirm the transaction belongs to this payment's reference.
        if (verification.Reference is not null && !string.Equals(verification.Reference, payment.TransactionReference, StringComparison.Ordinal))
            return "reference_mismatch";

        var outcome = await _settlement.ApplyAsync(payment, verification, "webhook", ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return outcome.Status.ToLowerInvariant();
    }
}
