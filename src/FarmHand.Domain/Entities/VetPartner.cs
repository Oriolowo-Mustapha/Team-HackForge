namespace FarmHand.Domain.Entities;

public class VetPartner : BaseEntity
{
    public Guid? UserId { get; protected set; }
    public string Name { get; protected set; } = string.Empty;
    public string Email { get; protected set; } = string.Empty;
    public string Specialization { get; protected set; } = string.Empty;
    public string Contact { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;

    public string? PayoutBankCode { get; protected set; }
    /// <summary>Encrypted at rest (AES-GCM); never returned to clients.</summary>
    public string? PayoutAccountNumber { get; protected set; }
    public string? PayoutAccountLast4 { get; protected set; }
    public string? PayoutAccountName { get; protected set; }
    public string? PayoutCountry { get; protected set; }
    public string? PaymentSubaccountId { get; protected set; }

    public bool HasPayoutAccount =>
        !string.IsNullOrWhiteSpace(PayoutBankCode) &&
        !string.IsNullOrWhiteSpace(PayoutAccountNumber) &&
        !string.IsNullOrWhiteSpace(PaymentSubaccountId);

    protected VetPartner() { }

    public VetPartner(Guid userId, string name, string email, string specialization, string contact)
    {
        UserId = userId;
        Name = name;
        Email = email.ToLowerInvariant();
        Specialization = specialization;
        Contact = contact;
    }

    public void UpdatePayoutAccount(
        string bankCode,
        string encryptedAccountNumber,
        string accountLast4,
        string accountName,
        string country,
        string? subaccountId)
    {
        PayoutBankCode = bankCode.Trim();
        PayoutAccountNumber = encryptedAccountNumber;
        PayoutAccountLast4 = accountLast4;
        PayoutAccountName = accountName.Trim();
        PayoutCountry = country.Trim().ToUpperInvariant();
        PaymentSubaccountId = subaccountId;
        MarkUpdated();
    }

    public void UpdateProfile(string name, string specialization, string contact)
    {
        Name = name;
        Specialization = specialization;
        Contact = contact;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkUpdated();
    }
}
