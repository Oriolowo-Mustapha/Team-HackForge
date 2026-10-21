namespace FarmHand.Domain.Entities;

public class VetPartner : BaseEntity
{
    public string Name { get; protected set; } = string.Empty;
    public string Specialization { get; protected set; } = string.Empty;
    public string Contact { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;

    protected VetPartner() { }

    public VetPartner(string name, string specialization, string contact)
    {
        Name = name;
        Specialization = specialization;
        Contact = contact;
    }
}
