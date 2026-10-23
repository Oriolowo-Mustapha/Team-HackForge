namespace FarmHand.Domain.Interfaces;

public interface IVetRequestRepository : IRepository<VetRequest>
{
    Task<IEnumerable<VetRequest>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
    Task<IEnumerable<VetRequest>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<VetRequest?> GetByIdWithDetailsAsync(Guid vetRequestId, CancellationToken ct = default);
    Task<PagedList<VetRequestRow>> SearchAsync(VetRequestSearch search, CancellationToken ct = default);
    Task<VetRequestRow?> GetRowAsync(Guid vetRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<VetRequest>> GetByIncidentIdsAsync(IEnumerable<Guid> incidentIds, CancellationToken ct = default);

    /// <summary>
    /// Atomically assigns a pending request to the vet. Returns false when another vet already
    /// claimed it or the request is no longer pending, so two vets can never both win.
    /// </summary>
    Task<bool> TryClaimAsync(Guid vetRequestId, Guid vetPartnerId, CancellationToken ct = default);
}
