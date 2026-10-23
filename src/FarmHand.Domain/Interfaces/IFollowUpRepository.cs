namespace FarmHand.Domain.Interfaces;

public interface IFollowUpRepository : IRepository<FollowUp>
{
    Task<IEnumerable<FollowUp>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
    Task<IReadOnlyList<FollowUp>> GetByIncidentIdsAsync(IEnumerable<Guid> incidentIds, CancellationToken ct = default);
}
