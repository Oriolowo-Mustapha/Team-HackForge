namespace FarmHand.Domain.Interfaces;

public interface IIncidentRepository : IRepository<Incident>
{
    Task<IEnumerable<Incident>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<IEnumerable<Incident>> GetOpenByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<Incident?> GetByIdWithActionsAsync(Guid incidentId, CancellationToken ct = default);
}
