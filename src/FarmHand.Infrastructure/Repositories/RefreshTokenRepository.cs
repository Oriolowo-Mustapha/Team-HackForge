using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.Services;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class RefreshTokenRepository : EfRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var hashedToken = RefreshTokenHasher.HashToken(token);
        return await DbSet.FirstOrDefaultAsync(t => t.Token == hashedToken, ct);
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

    public async Task<bool> TryRevokeAsync(Guid tokenId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var updated = await DbSet
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.UpdatedAt, now), ct);
        return updated == 1;
    }
}
