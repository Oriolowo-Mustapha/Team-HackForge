using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class LivestockRecordRepository : EfRepository<LivestockRecord>, ILivestockRecordRepository
{
    public LivestockRecordRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<LivestockRecord>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(l => l.FarmId == farmId).ToListAsync(ct);
    }
}
