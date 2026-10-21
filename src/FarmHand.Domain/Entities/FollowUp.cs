namespace FarmHand.Domain.Entities;

public class FollowUp : BaseEntity
{
    public Guid IncidentId { get; protected set; }
    public Outcome FarmerResponse { get; protected set; }
    public string? Notes { get; protected set; }
    public DateTime RecordedAt { get; protected set; } = DateTime.UtcNow;

    protected FollowUp() { }

    public FollowUp(Guid incidentId, Outcome farmerResponse, string? notes = null)
    {
        IncidentId = incidentId;
        FarmerResponse = farmerResponse;
        Notes = notes;
    }
}
