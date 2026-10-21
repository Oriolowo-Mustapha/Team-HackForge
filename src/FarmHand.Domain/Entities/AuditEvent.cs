namespace FarmHand.Domain.Entities;

public class AuditEvent : BaseEntity
{
    public string ActorId { get; protected set; } = string.Empty;
    public string EventType { get; protected set; } = string.Empty;
    public Guid? RelatedRecordId { get; protected set; }
    public DateTime Timestamp { get; protected set; } = DateTime.UtcNow;
    public string? Details { get; protected set; }

    protected AuditEvent() { }

    public AuditEvent(string actorId, string eventType, Guid? relatedRecordId = null, string? details = null)
    {
        ActorId = actorId;
        EventType = eventType;
        RelatedRecordId = relatedRecordId;
        Details = details;
    }
}
