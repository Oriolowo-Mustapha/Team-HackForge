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

    public async Task<IReadOnlyList<Farm>> ListByOwnerIdAsync(string ownerId, CancellationToken ct = default)
    {
        return await DbSet.Where(f => f.OwnerId == ownerId).OrderBy(f => f.CreatedAt).ToListAsync(ct);
    }

    public async Task<PagedList<Farm>> ListAllAsync(string? text, Paging paging, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{text.Trim()}%";
            query = query.Where(f => EF.Functions.ILike(f.DisplayName, pattern) || EF.Functions.ILike(f.Location, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(f => f.CreatedAt).Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);
        return new PagedList<Farm>(items, total);
    }
}
