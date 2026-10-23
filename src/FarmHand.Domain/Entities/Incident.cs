namespace FarmHand.Domain.Entities;

public class Incident : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public Guid? LivestockAnimalId { get; protected set; }
    public Guid? LivestockCategoryId { get; protected set; }
    public Guid? SourceObservationId { get; protected set; }
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
        Guid? livestockAnimalId,
        Guid? livestockCategoryId,
        DateTime detectedAt,
        string incidentType,
        Severity severity,
        string triggeringObservations,
        string evidenceSummary,
        string? ruleVersion = null,
        Guid? sourceObservationId = null)
    {
        FarmId = farmId;
        LivestockAnimalId = livestockAnimalId;
        LivestockCategoryId = livestockCategoryId;
        DetectedAt = detectedAt;
        IncidentType = incidentType;
        Severity = severity;
        TriggeringObservations = triggeringObservations;
        EvidenceSummary = evidenceSummary;
        RuleVersion = ruleVersion;
        SourceObservationId = sourceObservationId;
    }

    public bool IsClosed => Status == IncidentStatus.Resolved;

    /// <summary>The single source of truth for which status changes are valid from the current status.</summary>
    public static IReadOnlyList<IncidentStatus> AllowedTransitionsFrom(IncidentStatus status) => status switch
    {
        IncidentStatus.Open => [IncidentStatus.Acknowledged, IncidentStatus.UnderMonitoring, IncidentStatus.FollowUpRequired, IncidentStatus.Resolved],
        IncidentStatus.Acknowledged => [IncidentStatus.UnderMonitoring, IncidentStatus.FollowUpRequired, IncidentStatus.Resolved],
        IncidentStatus.UnderMonitoring => [IncidentStatus.FollowUpRequired, IncidentStatus.Resolved],
        IncidentStatus.FollowUpRequired => [IncidentStatus.UnderMonitoring, IncidentStatus.Resolved],
        IncidentStatus.Resolved => [IncidentStatus.Reopened],
        IncidentStatus.Reopened => [IncidentStatus.Acknowledged, IncidentStatus.UnderMonitoring, IncidentStatus.FollowUpRequired, IncidentStatus.Resolved],
        _ => []
    };

    public IReadOnlyList<IncidentStatus> AllowedTransitions => AllowedTransitionsFrom(Status);

    private void EnsureTransition(IncidentStatus target)
    {
        if (!AllowedTransitionsFrom(Status).Contains(target))
            throw new InvalidOperationException($"Cannot change an incident from {Status} to {target}.");
    }

    public void Acknowledge()
    {
        EnsureTransition(IncidentStatus.Acknowledged);
        Status = IncidentStatus.Acknowledged;
        MarkUpdated();
    }

    public void SetUnderMonitoring(DateTime? followUpDate = null)
    {
        EnsureTransition(IncidentStatus.UnderMonitoring);
        Status = IncidentStatus.UnderMonitoring;
        FollowUpDate = followUpDate;
        MarkUpdated();
    }

    public void RequireFollowUp(DateTime followUpDate)
    {
        EnsureTransition(IncidentStatus.FollowUpRequired);
        Status = IncidentStatus.FollowUpRequired;
        FollowUpDate = followUpDate;
        MarkUpdated();
    }

    public void Resolve()
    {
        EnsureTransition(IncidentStatus.Resolved);
        Status = IncidentStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        FollowUpDate = null;
        MarkUpdated();
    }

    public void Reopen()
    {
        EnsureTransition(IncidentStatus.Reopened);
        Status = IncidentStatus.Reopened;
        ResolvedAt = null;
        MarkUpdated();
    }

    /// <summary>Raises severity when new evidence is worse; severity is never silently lowered.</summary>
    public void Escalate(Severity severity)
    {
        if (severity <= Severity) return;
        Severity = severity;
        MarkUpdated();
    }
}
