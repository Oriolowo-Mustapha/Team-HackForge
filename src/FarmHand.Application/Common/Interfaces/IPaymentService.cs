namespace FarmHand.Application.Common.Interfaces;

public record PaymentResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage,
    string? CheckoutUrl = null,
    decimal? Amount = null,
    string? Currency = null);

public interface IPaymentService
{
    Task<PaymentResult> InitiatePaymentAsync(Guid vetRequestId, decimal amount, string currency, string customerEmail, string? customerName = null, CancellationToken ct = default);
    Task<PaymentResult> VerifyPaymentAsync(string transactionReference, CancellationToken ct = default);
}
