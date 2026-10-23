namespace FarmHand.Domain.Entities;

/// <summary>Single-use, short-lived password reset token. Only the SHA-256 hash is stored.</summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; protected set; }
    public string TokenHash { get; protected set; } = string.Empty;
    public DateTime ExpiresAt { get; protected set; }
    public DateTime? UsedAt { get; protected set; }

    protected PasswordResetToken() { }

    public PasswordResetToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsUsable => UsedAt is null && DateTime.UtcNow < ExpiresAt;

    public void MarkUsed()
    {
        UsedAt = DateTime.UtcNow;
        MarkUpdated();
    }
}
