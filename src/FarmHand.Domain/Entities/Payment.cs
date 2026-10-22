namespace FarmHand.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid VetRequestId { get; protected set; }
    public Money Amount { get; protected set; } = Money.Zero;
    public Money PlatformCommission { get; protected set; } = Money.Zero;
    public Money TotalAmount { get; protected set; } = Money.Zero;
    public Money VetPayoutAmount { get; protected set; } = Money.Zero;
    public string Status { get; protected set; } = "Pending";
    public string TransactionReference { get; protected set; } = string.Empty;
    public DateTime? PaidAt { get; protected set; }

    protected Payment() { }

    public Payment(Guid vetRequestId, Money amount, decimal platformCommissionPercentage)
    {
        VetRequestId = vetRequestId;
        Amount = amount;
        var commission = amount.Amount * platformCommissionPercentage / 100m;
        PlatformCommission = new Money(commission, amount.Currency);
        TotalAmount = new Money(amount.Amount + commission, amount.Currency);
        VetPayoutAmount = amount;
    }

    public void MarkPending(string transactionReference)
    {
        Status = "Pending";
        TransactionReference = transactionReference;
        MarkUpdated();
    }

    public void MarkCompleted(string transactionReference)
    {
        if (Status == "Completed" && TransactionReference == transactionReference)
            return;

        Status = "Completed";
        TransactionReference = transactionReference;
        PaidAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void MarkFailed()
    {
        Status = "Failed";
        MarkUpdated();
    }
}
