using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class FarmRepository : EfRepository<Farm>, IFarmRepository
{
    public FarmRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<Farm?> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(f => f.OwnerId == ownerId, ct);
    }
}
