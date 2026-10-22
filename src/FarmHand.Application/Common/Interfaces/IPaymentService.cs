namespace FarmHand.Application.Common.Interfaces;

public record PaymentResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage,
    string? CheckoutUrl = null,
    decimal? Amount = null,
    string? Currency = null);

/// <summary>
/// Split instructions for a payment: the vet's Flutterwave subaccount and the
/// platform commission rate (fraction of the total transaction) retained before settlement.
/// </summary>
public record PaymentSplit(string SubaccountId, decimal PlatformCommissionRate);

public record SubaccountResult(bool Success, string? SubaccountId, string? ErrorMessage, bool IsTransient = false);

public record VetSubaccountRequest(
    Guid VetPartnerId,
    string BusinessName,
    string Email,
    string Contact,
    string BankCode,
    string AccountNumber,
    string AccountName,
    string Country,
    string? ExistingSubaccountId = null);

public interface IPaymentService
{
    Task<PaymentResult> InitiatePaymentAsync(
        Guid vetRequestId,
        decimal amount,
        string currency,
        string customerEmail,
        string? customerName = null,
        PaymentSplit? split = null,
        CancellationToken ct = default);

    Task<PaymentResult> VerifyPaymentAsync(string transactionReference, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the vet's payment subaccount so visit payments can be
    /// split automatically. Returns a simulated id when the provider is not live.
    /// </summary>
    Task<SubaccountResult> CreateOrUpdateSubaccountAsync(VetSubaccountRequest request, CancellationToken ct = default);

    bool VerifyWebhookSignature(string rawBody, string? flutterwaveSignature, string? verifHash, string secretHash);
}
