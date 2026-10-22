namespace FarmHand.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; protected set; }
    public string Token { get; protected set; } = string.Empty;
    public DateTime ExpiresAt { get; protected set; }
    public DateTime? RevokedAt { get; protected set; }
    public string? ReplacedByToken { get; protected set; }

    protected RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTime expiresAt)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
    }

    public void Revoke(string? replacedByToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByToken = replacedByToken;
        MarkUpdated();
    }

    public bool IsActive => !RevokedAt.HasValue && DateTime.UtcNow < ExpiresAt;
}
