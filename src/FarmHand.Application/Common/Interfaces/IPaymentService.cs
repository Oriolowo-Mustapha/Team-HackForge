namespace FarmHand.Application.Common.Interfaces;

public record PaymentResult(
    bool Success,
    string? TransactionReference,
    string? ErrorMessage,
    string? CheckoutUrl = null,
    decimal? Amount = null,
    string? Currency = null);

public enum PaymentVerificationOutcome
{
    /// <summary>The provider confirms the charge succeeded.</summary>
    Successful,

    /// <summary>The provider reports the charge failed or was cancelled.</summary>
    Failed,

    /// <summary>The checkout has not been completed yet.</summary>
    Pending,

    /// <summary>The provider has no transaction for this reference.</summary>
    NotFound,

    /// <summary>The provider could not be reached; the status is unknown.</summary>
    Error
}

/// <summary>Provider-verified state of a transaction. Amount and currency come from the provider, not the client.</summary>
public record PaymentVerification(
    PaymentVerificationOutcome Outcome,
    decimal? Amount,
    string? Currency,
    string? ProviderTransactionId,
    string? Reference,
    string? Message);

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
    /// <summary>"Live", "Simulated" or "Disabled". Payments are disabled unless a mode is configured explicitly.</summary>
    string Mode { get; }

    bool IsEnabled => Mode is "Live" or "Simulated";

    Task<PaymentResult> InitiatePaymentAsync(
        Guid vetRequestId,
        decimal amount,
        string currency,
        string customerEmail,
        string? customerName = null,
        PaymentSplit? split = null,
        CancellationToken ct = default);

    /// <summary>Verifies a transaction by the merchant reference (tx_ref) created at initiation.</summary>
    Task<PaymentVerification> VerifyByReferenceAsync(string reference, CancellationToken ct = default);

    /// <summary>Verifies a transaction by the provider's transaction id (from webhooks or redirects).</summary>
    Task<PaymentVerification> VerifyByTransactionIdAsync(string transactionId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the vet's payment subaccount so visit payments can be
    /// split automatically. Returns a simulated id when the provider is not live.
    /// </summary>
    Task<SubaccountResult> CreateOrUpdateSubaccountAsync(VetSubaccountRequest request, CancellationToken ct = default);

    bool VerifyWebhookSignature(string rawBody, string? flutterwaveSignature, string? verifHash, string secretHash);
}
