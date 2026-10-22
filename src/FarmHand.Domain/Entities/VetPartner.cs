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
    public string? PayoutAccountNumber { get; protected set; }
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
        string accountNumber,
        string accountName,
        string country,
        string? subaccountId)
    {
        PayoutBankCode = bankCode.Trim();
        PayoutAccountNumber = accountNumber.Trim();
        PayoutAccountName = accountName.Trim();
        PayoutCountry = country.Trim().ToUpperInvariant();
        PaymentSubaccountId = subaccountId;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }
}
