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
}
