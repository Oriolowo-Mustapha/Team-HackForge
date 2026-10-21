namespace FarmHand.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; }

    protected void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
    protected void MarkDeleted() => IsDeleted = true;
}
