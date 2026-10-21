using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class IncidentRepository : EfRepository<Incident>, IIncidentRepository
{
    public IncidentRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<Incident>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(i => i.FarmId == farmId).OrderByDescending(i => i.DetectedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetOpenByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(i => i.FarmId == farmId && i.Status != Domain.Enums.IncidentStatus.Resolved).ToListAsync(ct);
    }

    public async Task<Incident?> GetByIdWithActionsAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(i => i.Id == incidentId, ct);
    }
}
