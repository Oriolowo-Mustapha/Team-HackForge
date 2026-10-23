namespace FarmHand.Domain.Entities;

public class VetRequest : BaseEntity
{
    public Guid IncidentId { get; protected set; }
    public Guid? VetPartnerId { get; protected set; }
    public RequestType RequestType { get; protected set; }
    public RequestStatus Status { get; protected set; } = RequestStatus.Pending;
    public Money FarmerCost { get; protected set; } = Money.Zero;
    public string? Description { get; protected set; }
    public DateTime? PreferredVisitDate { get; protected set; }
    public Guid? RequestedByUserId { get; protected set; }
    public DateTime? RespondedAt { get; protected set; }
    public DateTime? CompletedAt { get; protected set; }
    public string? CompletionNotes { get; protected set; }
    public DateTime? CancelledAt { get; protected set; }
    public DateTime? DeclinedAt { get; protected set; }
    public string? DeclineReason { get; protected set; }

    protected VetRequest() { }

    public VetRequest(
        Guid incidentId,
        RequestType requestType,
        Money farmerCost,
        Guid? vetPartnerId = null,
        string? description = null,
        DateTime? preferredVisitDate = null,
        Guid? requestedByUserId = null)
    {
        IncidentId = incidentId;
        RequestType = requestType;
        FarmerCost = farmerCost;
        VetPartnerId = vetPartnerId;
        Description = description;
        PreferredVisitDate = preferredVisitDate;
        RequestedByUserId = requestedByUserId;
    }

    /// <summary>True while the request still needs or has a vet working on it.</summary>
    public bool IsActive => Status is RequestStatus.Pending or RequestStatus.Responded;

    public bool IsTerminal => Status is RequestStatus.Completed or RequestStatus.Cancelled or RequestStatus.Declined;

    /// <summary>Assigns the responding vet. Concurrent claims are additionally guarded by an atomic database update.</summary>
    public void Respond(Guid? vetPartnerId = null)
    {
        if (Status != RequestStatus.Pending)
            throw new InvalidOperationException("This vet request has already been assigned to a vet.");

        if (VetPartnerId is { } assigned && vetPartnerId is { } responder && assigned != responder)
            throw new InvalidOperationException("This request is assigned to another vet.");

        Status = RequestStatus.Responded;
        VetPartnerId = vetPartnerId ?? VetPartnerId;
        RespondedAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Decline(string? reason = null)
    {
        if (Status != RequestStatus.Pending)
            throw new InvalidOperationException("Only a pending request can be declined.");

        Status = RequestStatus.Declined;
        DeclinedAt = DateTime.UtcNow;
        DeclineReason = reason;
        MarkUpdated();
    }

    public void Cancel()
    {
        if (!IsActive)
            throw new InvalidOperationException("Only a pending or accepted request can be cancelled.");

        Status = RequestStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Complete(string? notes = null)
    {
        if (Status != RequestStatus.Responded)
            throw new InvalidOperationException("Only an accepted request can be completed.");

        Status = RequestStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        CompletionNotes = notes;
        MarkUpdated();
    }
}
