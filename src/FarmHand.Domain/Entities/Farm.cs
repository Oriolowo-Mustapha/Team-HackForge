namespace FarmHand.Domain.Entities;

public class Farm : BaseEntity
{
    public string OwnerId { get; protected set; } = string.Empty;
    public string DisplayName { get; protected set; } = string.Empty;
    public string Location { get; protected set; } = string.Empty;

    protected Farm() { }

    public Farm(string ownerId, string displayName, string location)
    {
        OwnerId = ownerId;
        DisplayName = displayName;
        Location = location;
    }

    public void UpdateDetails(string displayName, string location)
    {
        DisplayName = displayName;
        Location = location;
        MarkUpdated();
    }
}
