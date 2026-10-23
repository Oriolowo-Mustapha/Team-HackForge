using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class UserRepository : EfRepository<User>, IUserRepository
{
    public UserRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);
    }

    public async Task<IEnumerable<User>> GetByRoleAsync(string role, CancellationToken ct = default)
    {
        return await DbSet.Where(u => u.Role == role).ToListAsync(ct);
    }

    public async Task<PagedList<User>> SearchAsync(UserSearch search, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Role)) query = query.Where(u => u.Role == search.Role);
        if (search.IsActive is { } active) query = query.Where(u => u.IsActive == active);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = $"%{search.Text.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern) || (u.FullName != null && EF.Functions.ILike(u.FullName, pattern)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt).Skip(search.Paging.Skip).Take(search.Paging.PageSize).ToListAsync(ct);
        return new PagedList<User>(items, total);
    }

}
