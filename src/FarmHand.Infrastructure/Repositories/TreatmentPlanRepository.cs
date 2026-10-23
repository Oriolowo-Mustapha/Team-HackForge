using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class TreatmentPlanRepository : EfRepository<TreatmentPlan>, ITreatmentPlanRepository
{
    public TreatmentPlanRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<TreatmentPlan?> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.VetRequestId == vetRequestId, ct);
    }

    public async Task<IReadOnlyList<TreatmentPlan>> GetByVetRequestIdsAsync(IEnumerable<Guid> vetRequestIds, CancellationToken ct = default)
    {
        var ids = vetRequestIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(t => ids.Contains(t.VetRequestId)).OrderBy(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TreatmentScheduleItem>> GetScheduleItemsAsync(IEnumerable<Guid> planIds, CancellationToken ct = default)
    {
        var ids = planIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbContext.Set<TreatmentScheduleItem>().Where(s => ids.Contains(s.TreatmentPlanId)).OrderBy(s => s.DueDate).ToListAsync(ct);
    }

    public async Task<TreatmentScheduleItem?> GetScheduleItemAsync(Guid itemId, CancellationToken ct = default)
    {
        return await DbContext.Set<TreatmentScheduleItem>().FirstOrDefaultAsync(s => s.Id == itemId, ct);
    }

    public async Task AddScheduleItemAsync(TreatmentScheduleItem item, CancellationToken ct = default)
    {
        await DbContext.Set<TreatmentScheduleItem>().AddAsync(item, ct);
    }
}
