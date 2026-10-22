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

    protected AIDiagnosis() { }

    public AIDiagnosis(
        Guid observationId,
        Severity severity,
        string diagnosisSummary,
        string recommendedAction,
        bool requiresVetConsultation,
        string? rawAssessment = null)
    {
        ObservationId = observationId;
        Severity = severity;
        DiagnosisSummary = diagnosisSummary;
        RecommendedAction = recommendedAction;
        RequiresVetConsultation = requiresVetConsultation;
        IncidentCreated = false;
        RawAssessment = rawAssessment;
    }

    public void LinkIncident(Guid incidentId)
    {
        IncidentId = incidentId;
        IncidentCreated = true;
        MarkUpdated();
    }
}
