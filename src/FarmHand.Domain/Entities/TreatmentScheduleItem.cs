namespace FarmHand.Domain.Entities;

public class TreatmentScheduleItem : BaseEntity
{
    public Guid TreatmentPlanId { get; protected set; }
    public string Action { get; protected set; } = string.Empty;
    public DateTime DueDate { get; protected set; }
    public bool IsCompleted { get; protected set; }
    public DateTime? CompletedAt { get; protected set; }

    protected TreatmentScheduleItem() { }

    public TreatmentScheduleItem(Guid treatmentPlanId, string action, DateTime dueDate)
    {
        TreatmentPlanId = treatmentPlanId;
        Action = action;
        DueDate = dueDate;
    }

    public void MarkCompleted()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        MarkUpdated();
    }
}
