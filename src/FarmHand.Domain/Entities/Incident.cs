namespace FarmHand.Domain.Entities;

public class Incident : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public Guid FlockId { get; protected set; }
    public DateTime DetectedAt { get; protected set; }
    public string IncidentType { get; protected set; } = string.Empty;
    public Severity Severity { get; protected set; }
    public string TriggeringObservations { get; protected set; } = string.Empty;
    public string EvidenceSummary { get; protected set; } = string.Empty;
    public IncidentStatus Status { get; protected set; } = IncidentStatus.Open;
    public string? RuleVersion { get; protected set; }
    public DateTime? FollowUpDate { get; protected set; }
    public DateTime? ResolvedAt { get; protected set; }

    protected Incident() { }

    public Incident(
        Guid farmId,
        Guid flockId,
        DateTime detectedAt,
        string incidentType,
        Severity severity,
        string triggeringObservations,
        string evidenceSummary,
        string? ruleVersion = null)
    {
        FarmId = farmId;
        FlockId = flockId;
        DetectedAt = detectedAt;
        IncidentType = incidentType;
        Severity = severity;
        TriggeringObservations = triggeringObservations;
        EvidenceSummary = evidenceSummary;
        RuleVersion = ruleVersion;
    }

    public void Acknowledge()
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot acknowledge incident in status {Status}.");

        Status = IncidentStatus.Acknowledged;
        MarkUpdated();
    }

    public void SetUnderMonitoring(DateTime? followUpDate = null)
    {
        Status = IncidentStatus.UnderMonitoring;
        FollowUpDate = followUpDate;
        MarkUpdated();
    }

    public void RequireFollowUp(DateTime followUpDate)
    {
        Status = IncidentStatus.FollowUpRequired;
        FollowUpDate = followUpDate;
        MarkUpdated();
    }

    public void Resolve()
    {
        Status = IncidentStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Reopen()
    {
        Status = IncidentStatus.Reopened;
        ResolvedAt = null;
        MarkUpdated();
    }
}
