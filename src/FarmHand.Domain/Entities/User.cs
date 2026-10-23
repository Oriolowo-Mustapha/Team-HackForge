namespace FarmHand.Domain.Entities;

/// <summary>Role names stored on <see cref="User.Role"/>.</summary>
public static class UserRoles
{
    public const string Farmer = "Farmer";
    public const string Vet = "Vet";
    public const string Admin = "Admin";
}

public class User : BaseEntity
{
    public string Email { get; protected set; } = string.Empty;
    public string PasswordHash { get; protected set; } = string.Empty;
    public string Role { get; protected set; } = UserRoles.Farmer;
    public string? FullName { get; protected set; }
    public string? PhoneNumber { get; protected set; }
    public Guid? FarmId { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public int TokenVersion { get; protected set; } = 0;

    /// <summary>Set for accounts created with an administrator-issued temporary password.</summary>
    public bool MustChangePassword { get; protected set; }
    public DateTime? LastLoginAt { get; protected set; }

    protected User() { }

    public User(string email, string passwordHash, string role = UserRoles.Farmer, string? fullName = null, string? phoneNumber = null, Guid? farmId = null)
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
        MustChangePassword = false;
        MarkUpdated();
    }

    /// <summary>Sets an administrator-issued temporary password that must be replaced at next sign-in.</summary>
    public void SetTemporaryPassword(string temporaryPasswordHash)
    {
        PasswordHash = temporaryPasswordHash;
        MustChangePassword = true;
        MarkUpdated();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
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

    public void Activate()
    {
        IsActive = true;
        MarkUpdated();
    }

    public void IncrementTokenVersion()
    {
        TokenVersion++;
        MarkUpdated();
    }
}
