namespace FarmHand.Domain.Entities;

public class LivestockCategory : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public string Name { get; protected set; } = string.Empty; // e.g. "Poultry", "Goats", "Cattle"
    public string? Description { get; protected set; }
    public int AnimalCount { get; protected set; }

    protected LivestockCategory() { }

    public LivestockCategory(Guid farmId, string name, string? description = null)
    {
        FarmId = farmId;
        Name = name;
        Description = description;
    }

    public void UpdateDetails(string name, string? description = null)
    {
        Name = name;
        Description = description;
        MarkUpdated();
    }

    public void IncrementAnimalCount() => AnimalCount++;
    public void DecrementAnimalCount() => AnimalCount = Math.Max(0, AnimalCount - 1);
}
