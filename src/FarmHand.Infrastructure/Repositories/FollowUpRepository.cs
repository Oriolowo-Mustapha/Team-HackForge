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
}
