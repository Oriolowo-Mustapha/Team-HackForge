using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class PreventionPlanRepository : EfRepository<PreventionPlan>, IPreventionPlanRepository
{
    public PreventionPlanRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<PreventionPlan?> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(p => p.IncidentId == incidentId, ct);
    }
}
