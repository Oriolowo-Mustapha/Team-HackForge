namespace FarmHand.Domain.Entities;

public class CostShare : BaseEntity
{
    public Guid VetRequestId { get; protected set; }
    public string PayerType { get; protected set; } = string.Empty; // farmer, coop, offtaker, insurer
    public Money Amount { get; protected set; } = Money.Zero;
    public DateTime CommittedAt { get; protected set; } = DateTime.UtcNow;

    protected CostShare() { }

    public CostShare(Guid vetRequestId, string payerType, Money amount)
    {
        VetRequestId = vetRequestId;
        PayerType = payerType;
        Amount = amount;
    }
}
