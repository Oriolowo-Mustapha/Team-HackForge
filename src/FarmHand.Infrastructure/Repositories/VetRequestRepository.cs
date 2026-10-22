using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class VetRequestRepository : EfRepository<VetRequest>, IVetRequestRepository
{
    public VetRequestRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<VetRequest>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.Where(v => v.IncidentId == incidentId).ToListAsync(ct);
    }

    public async Task<IEnumerable<VetRequest>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet
            .Join(
                DbContext.Set<Incident>(),
                v => v.IncidentId,
                i => i.Id,
                (v, i) => new { v, i })
            .Where(x => x.i.FarmId == farmId)
            .Select(x => x.v)
            .ToListAsync(ct);
    }

    public async Task<VetRequest?> GetByIdWithDetailsAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(v => v.Id == vetRequestId, ct);
    }
}
