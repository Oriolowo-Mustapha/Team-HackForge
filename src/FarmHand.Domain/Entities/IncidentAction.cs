namespace FarmHand.Domain.Entities;

public class IncidentAction : BaseEntity
{
    public Guid IncidentId { get; protected set; }
    public string Description { get; protected set; } = string.Empty;
    public string Priority { get; protected set; } = string.Empty;
    public string Status { get; protected set; } = "Pending";
    public DateTime? CompletedAt { get; protected set; }
    public string? CompletionNotes { get; protected set; }

    protected IncidentAction() { }

    public IncidentAction(Guid incidentId, string description, string priority)
    {
        IncidentId = incidentId;
        Description = description;
        Priority = priority;
    }

    public void Complete(string? notes = null)
    {
        Status = "Completed";
        CompletedAt = DateTime.UtcNow;
        CompletionNotes = notes;
        MarkUpdated();
    }
}
