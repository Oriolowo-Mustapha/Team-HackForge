namespace FarmHand.Application.Common.Interfaces;

public record PaymentResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage,
    decimal? Amount = null,
    string? Currency = null);

public interface IPaymentService
{
    Task<PaymentResult> InitiatePaymentAsync(Guid vetRequestId, decimal amount, string currency, CancellationToken ct = default);
    Task<PaymentResult> VerifyPaymentAsync(string transactionReference, CancellationToken ct = default);
}
