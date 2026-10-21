namespace FarmHand.Domain.Entities;

public class TreatmentPlan : BaseEntity
{
    public Guid VetRequestId { get; protected set; }
    public string AuthoredByVetId { get; protected set; } = string.Empty;
    public string InstructionsText { get; protected set; } = string.Empty;
    public string? Language { get; protected set; }

    protected TreatmentPlan() { }

    public TreatmentPlan(Guid vetRequestId, string authoredByVetId, string instructionsText, string? language = null)
    {
        VetRequestId = vetRequestId;
        AuthoredByVetId = authoredByVetId;
        InstructionsText = instructionsText;
        Language = language;
    }
}
