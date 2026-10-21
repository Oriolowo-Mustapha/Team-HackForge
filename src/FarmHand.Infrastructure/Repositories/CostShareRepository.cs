using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class CostShareRepository : EfRepository<CostShare>, ICostShareRepository
{
    public CostShareRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<CostShare>> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        return await DbSet.Where(c => c.VetRequestId == vetRequestId).ToListAsync(ct);
    }
}
