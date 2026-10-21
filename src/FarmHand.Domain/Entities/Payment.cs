namespace FarmHand.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid VetRequestId { get; protected set; }
    public Money Amount { get; protected set; } = Money.Zero;
    public string Status { get; protected set; } = "Pending";
    public string TransactionReference { get; protected set; } = string.Empty;
    public DateTime? PaidAt { get; protected set; }

    protected Payment() { }

    public Payment(Guid vetRequestId, Money amount)
    {
        VetRequestId = vetRequestId;
        Amount = amount;
    }

    public void MarkCompleted(string transactionReference)
    {
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
