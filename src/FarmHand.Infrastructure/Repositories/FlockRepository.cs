using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class FlockRepository : EfRepository<Flock>, IFlockRepository
{
    public FlockRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<Flock>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(f => f.FarmId == farmId).ToListAsync(ct);
    }
}
