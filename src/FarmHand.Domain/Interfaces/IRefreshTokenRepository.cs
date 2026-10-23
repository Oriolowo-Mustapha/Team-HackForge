namespace FarmHand.Domain.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Atomically revokes an active token; returns false if it was already revoked (reuse or a concurrent refresh).</summary>
    Task<bool> TryRevokeAsync(Guid tokenId, CancellationToken ct = default);
}
