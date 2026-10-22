using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class RefreshTokenRepository : EfRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet.Where(t => t.UserId == userId).ToListAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await DbSet.Where(t => t.UserId == userId && !t.RevokedAt.HasValue).ToListAsync(ct);
        foreach (var token in tokens)
            token.Revoke();
    }
}
