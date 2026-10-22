namespace FarmHand.Application.Common.Interfaces;

public interface IResourceAuthorizationService
{
    Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken ct = default);
    Task EnsureFarmAccessAsync(Guid farmId, CancellationToken ct = default);
    Task EnsureIncidentAccessAsync(Guid incidentId, CancellationToken ct = default);
    Task EnsureObservationAccessAsync(Guid observationId, CancellationToken ct = default);
    Task EnsureVetRequestAccessAsync(Guid vetRequestId, CancellationToken ct = default);
    Task EnsureAnimalAccessAsync(Guid animalId, CancellationToken ct = default);
    Task EnsureCategoryAccessAsync(Guid categoryId, CancellationToken ct = default);
    void EnsureVetOrAdmin();
}
