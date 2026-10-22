namespace FarmHand.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; protected set; } = string.Empty;
    public string PasswordHash { get; protected set; } = string.Empty;
    public string Role { get; protected set; } = "Farmer"; // Farmer, Admin, Vet
    public string? FullName { get; protected set; }
    public string? PhoneNumber { get; protected set; }
    public Guid? FarmId { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public int TokenVersion { get; protected set; } = 0;

    protected User() { }

    public User(string email, string passwordHash, string role = "Farmer", string? fullName = null, string? phoneNumber = null, Guid? farmId = null)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        FarmId = farmId;
    }

    public void UpdateProfile(string? fullName = null, string? phoneNumber = null)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        MarkUpdated();
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        MarkUpdated();
    }

    public void AssignFarm(Guid farmId)
    {
        FarmId = farmId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        IncrementTokenVersion();
        MarkUpdated();
    }

    public void IncrementTokenVersion()
    {
        TokenVersion++;
        MarkUpdated();
    }
}
