using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class FollowUpRepository : EfRepository<FollowUp>, IFollowUpRepository
{
    public FollowUpRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<FollowUp>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.Where(f => f.IncidentId == incidentId).OrderByDescending(f => f.RecordedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FollowUp>> GetByIncidentIdsAsync(IEnumerable<Guid> incidentIds, CancellationToken ct = default)
    {
        var ids = incidentIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(f => ids.Contains(f.IncidentId)).OrderByDescending(f => f.RecordedAt).ToListAsync(ct);
    }
}
