namespace FarmHand.Domain.Entities;

public class VetPartner : BaseEntity
{
    public Guid? UserId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string Email { get; protected set; } = string.Empty;
    public string Specialization { get; protected set; } = string.Empty;
    public string Contact { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;

    protected VetPartner() { }

    public VetPartner(Guid userId, string name, string email, string specialization, string contact)
    {
        UserId = userId;
        Name = name;
        Email = email.ToLowerInvariant();
        Specialization = specialization;
        Contact = contact;
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
