namespace FarmHand.Domain.Entities;

public class VetRequest : BaseEntity
{
    public Guid IncidentId { get; protected set; }
    public Guid? VetPartnerId { get; protected set; }
    public RequestType RequestType { get; protected set; }
    public RequestStatus Status { get; protected set; } = RequestStatus.Pending;
    public Money FarmerCost { get; protected set; } = Money.Zero;
    public DateTime? RespondedAt { get; protected set; }

    protected VetRequest() { }

    public VetRequest(
        Guid incidentId,
        RequestType requestType,
        Money farmerCost,
        Guid? vetPartnerId = null)
    {
        IncidentId = incidentId;
        RequestType = requestType;
        FarmerCost = farmerCost;
        VetPartnerId = vetPartnerId;
    }

    public void Respond(Guid? vetPartnerId = null)
    {
        Status = RequestStatus.Responded;
        VetPartnerId = vetPartnerId;
        RespondedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Complete()
    {
        Status = RequestStatus.Completed;
        MarkUpdated();
    }
}
