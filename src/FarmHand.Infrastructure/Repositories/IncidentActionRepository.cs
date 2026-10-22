using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class IncidentActionRepository : EfRepository<IncidentAction>, IIncidentActionRepository
{
    public IncidentActionRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<IncidentAction>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.IncidentId == incidentId).OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }
}
