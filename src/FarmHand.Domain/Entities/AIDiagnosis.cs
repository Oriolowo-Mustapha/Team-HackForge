namespace FarmHand.Domain.Entities;

public class AIDiagnosis : BaseEntity
{
    public Guid ObservationId { get; protected set; }
    public Guid? IncidentId { get; protected set; }
    public Severity Severity { get; protected set; }
    public string DiagnosisSummary { get; protected set; } = string.Empty;
    public string RecommendedAction { get; protected set; } = string.Empty;

    /// <summary>JSON array of practical care steps the farmer can do at the farm level, no vet required.</summary>
    public string? FarmerActionsJson { get; protected set; }
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
        string? source = null,
        string? farmerActionsJson = null)
    {
        ObservationId = observationId;
        Severity = severity;
        DiagnosisSummary = diagnosisSummary;
        RecommendedAction = recommendedAction;
        FarmerActionsJson = farmerActionsJson;
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
