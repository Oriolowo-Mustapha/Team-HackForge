namespace FarmHand.Domain.Interfaces;

public interface IFollowUpRepository : IRepository<FollowUp>
{
    Task<IEnumerable<FollowUp>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
}
