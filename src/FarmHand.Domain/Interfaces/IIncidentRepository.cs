namespace FarmHand.Domain.Interfaces;

public interface IIncidentRepository : IRepository<Incident>
{
    Task<IEnumerable<Incident>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetOpenByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<Incident?> GetByIdWithActionsAsync(Guid incidentId, CancellationToken ct = default);
    Task<PagedList<Incident>> SearchAsync(IncidentSearch search, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetBySourceObservationIdsAsync(IEnumerable<Guid> observationIds, CancellationToken ct = default);
}
