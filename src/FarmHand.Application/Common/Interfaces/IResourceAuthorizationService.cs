namespace FarmHand.Application.Common.Interfaces;

/// <summary>How the current user relates to an incident (and its farm).</summary>
public enum IncidentAccess
{
    None,

    /// <summary>A vet who may see a limited summary because a pending request is open to them.</summary>
    ProspectiveVet,

    /// <summary>A vet with an accepted (or completed) request on the incident.</summary>
    AssignedVet,

    Owner,
    Admin
}

public interface IResourceAuthorizationService
{
    Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken ct = default);

    /// <summary>Owner or admin. Vets never get farm-wide access.</summary>
    Task EnsureFarmAccessAsync(Guid farmId, CancellationToken ct = default);

    /// <summary>Farm ids the caller owns; null for administrators (all farms).</summary>
    Task<IReadOnlyList<Guid>?> GetAccessibleFarmIdsAsync(CancellationToken ct = default);

    /// <summary>The active vet profile linked to the caller, if any.</summary>
    Task<VetPartner?> GetCurrentVetAsync(CancellationToken ct = default);

    Task<IncidentAccess> GetIncidentAccessAsync(Guid incidentId, CancellationToken ct = default);

    /// <summary>Read access to an incident's full case: owner, admin or assigned vet.</summary>
    Task EnsureIncidentAccessAsync(Guid incidentId, CancellationToken ct = default);

    /// <summary>Write access to an incident (status, actions, follow-ups): owner, admin or assigned vet.</summary>
    Task EnsureIncidentWriteAccessAsync(Guid incidentId, CancellationToken ct = default);

    Task EnsureObservationAccessAsync(Guid observationId, CancellationToken ct = default);

    /// <summary>Owner, admin, assigned vet, or a vet the pending request is open to.</summary>
    Task EnsureVetRequestAccessAsync(Guid vetRequestId, CancellationToken ct = default);

    Task EnsureAnimalAccessAsync(Guid animalId, CancellationToken ct = default);
    Task EnsureCategoryAccessAsync(Guid categoryId, CancellationToken ct = default);
    void EnsureVetOrAdmin();
}
