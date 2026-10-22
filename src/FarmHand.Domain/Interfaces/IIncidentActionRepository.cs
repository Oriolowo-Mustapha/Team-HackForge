namespace FarmHand.Domain.Interfaces;

public interface IIncidentActionRepository : IRepository<IncidentAction>
{
    Task<IEnumerable<IncidentAction>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
}
