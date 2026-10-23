namespace FarmHand.Domain.Entities;

/// <summary>String status values persisted on <see cref="Payment.Status"/>.</summary>
public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

/// <summary>How a payment was processed: through the live provider or the labelled simulator.</summary>
public static class PaymentMode
{
    public const string Live = "Live";
    public const string Simulated = "Simulated";
}

public class Payment : BaseEntity
{
    public Guid VetRequestId { get; protected set; }
    public Money Amount { get; protected set; } = Money.Zero;
    public Money PlatformCommission { get; protected set; } = Money.Zero;
    public Money TotalAmount { get; protected set; } = Money.Zero;
    public Money VetPayoutAmount { get; protected set; } = Money.Zero;
    public string Status { get; protected set; } = PaymentStatus.Pending;
    public string Mode { get; protected set; } = PaymentMode.Simulated;
    public string TransactionReference { get; protected set; } = string.Empty;
    public string? ProviderTransactionId { get; protected set; }
    public string? CheckoutUrl { get; protected set; }
    public string? IdempotencyKey { get; protected set; }
    public string? SubaccountId { get; protected set; }
    public bool VetSubaccountUsed { get; protected set; }
    public string? FailureReason { get; protected set; }
    public Guid? InitiatedByUserId { get; protected set; }
    public DateTime? PaidAt { get; protected set; }

    protected Payment() { }

    public Payment(Guid vetRequestId, Money amount, decimal platformCommissionPercentage, string mode = PaymentMode.Simulated, Guid? initiatedByUserId = null)
    {
        VetRequestId = vetRequestId;
        // Owned value objects must not share an instance between navigations, so every
        // money field gets its own Money object (EF Core rejects shared owned instances).
        Amount = new Money(amount.Amount, amount.Currency);
        var commission = new Money(amount.Amount * platformCommissionPercentage / 100m, amount.Currency);
        PlatformCommission = commission;
        TotalAmount = new Money(amount.Amount + commission.Amount, amount.Currency);
        VetPayoutAmount = new Money(amount.Amount, amount.Currency);
        Mode = mode;
        InitiatedByUserId = initiatedByUserId;
    }

    public bool IsSimulated => Mode == PaymentMode.Simulated;

    public void MarkPending(
        string transactionReference,
        string? checkoutUrl = null,
        string? idempotencyKey = null,
        bool vetSubaccountUsed = false,
        string? subaccountId = null)
    {
        Status = PaymentStatus.Pending;
        TransactionReference = transactionReference;
        CheckoutUrl = checkoutUrl;
        IdempotencyKey = idempotencyKey;
        VetSubaccountUsed = vetSubaccountUsed;
        SubaccountId = subaccountId;
        MarkUpdated();
    }

    /// <summary>Marks the payment completed. The reference is never rewritten; the provider id is stored separately.</summary>
    public void MarkCompleted(string? providerTransactionId = null)
    {
        if (Status == PaymentStatus.Completed)
            return;

        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"A {Status.ToLowerInvariant()} payment cannot be completed.");

        Status = PaymentStatus.Completed;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
            ProviderTransactionId = providerTransactionId;
        FailureReason = null;
        PaidAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarkFailed(string? reason = null)
    {
        if (Status == PaymentStatus.Completed)
            throw new InvalidOperationException("A completed payment cannot be marked as failed.");

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        MarkUpdated();
    }

    public void Cancel()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only a pending payment can be cancelled.");

        Status = PaymentStatus.Cancelled;
        MarkUpdated();
    }
}
