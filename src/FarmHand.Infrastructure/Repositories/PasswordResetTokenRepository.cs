using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class PasswordResetTokenRepository : EfRepository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await DbSet.Where(t => t.UserId == userId && t.UsedAt == null).ToListAsync(ct);
        foreach (var token in tokens)
            token.MarkUsed();
    }
}
