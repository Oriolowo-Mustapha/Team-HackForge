namespace FarmHand.Domain.Entities;

public class AIDiagnosis : BaseEntity
{
    public Guid ObservationId { get; protected set; }
    public Guid? IncidentId { get; protected set; }
    public Severity Severity { get; protected set; }
    public string DiagnosisSummary { get; protected set; } = string.Empty;
    public string RecommendedAction { get; protected set; } = string.Empty;
    public bool RequiresVetConsultation { get; protected set; }
    public bool IncidentCreated { get; protected set; }
    public string? RawAssessment { get; protected set; }

    /// <summary>Who produced the assessment, e.g. "gemini:gemini-3.5-flash" or "rules" for the rule-based fallback.</summary>
    public string? Source { get; protected set; }

    protected AIDiagnosis() { }

    public AIDiagnosis(
        Guid observationId,
        Severity severity,
        string diagnosisSummary,
        string recommendedAction,
        bool requiresVetConsultation,
        string? rawAssessment = null,
        string? source = null)
    {
        ObservationId = observationId;
        Severity = severity;
        DiagnosisSummary = diagnosisSummary;
        RecommendedAction = recommendedAction;
        RequiresVetConsultation = requiresVetConsultation;
        IncidentCreated = false;
        RawAssessment = rawAssessment;
        Source = source;
    }

    public void LinkIncident(Guid incidentId)
    {
        IncidentId = incidentId;
        IncidentCreated = true;
        MarkUpdated();
    }
}
