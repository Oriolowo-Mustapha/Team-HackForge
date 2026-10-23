namespace FarmHand.Domain.Enums;

/// <summary>Vet request lifecycle. Values are persisted as integers; append new values only.</summary>
public enum RequestStatus
{
    /// <summary>Waiting for a vet to accept.</summary>
    Pending = 0,

    /// <summary>A vet accepted the request and is assigned to it.</summary>
    Responded = 1,

    /// <summary>The assigned vet (or an admin) closed the request after the service was delivered.</summary>
    Completed = 2,

    /// <summary>The farmer withdrew the request before it was completed.</summary>
    Cancelled = 3,

    /// <summary>The vet the farmer selected declined the request.</summary>
    Declined = 4
}
