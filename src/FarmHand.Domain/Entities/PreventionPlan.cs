namespace FarmHand.Domain.Entities;

public class PreventionPlan : BaseEntity
{
    public Guid IncidentId { get; protected set; }
    public string Content { get; protected set; } = string.Empty;
    public bool GeneratedFromHistory { get; protected set; }

    protected PreventionPlan() { }

    public PreventionPlan(Guid incidentId, string content, bool generatedFromHistory = true)
    {
        IncidentId = incidentId;
        Content = content;
        GeneratedFromHistory = generatedFromHistory;
    }

    public void UpdateContent(string content, bool generatedFromHistory)
    {
        Content = content;
        GeneratedFromHistory = generatedFromHistory;
        MarkUpdated();
    }
}
