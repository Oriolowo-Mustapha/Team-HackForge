namespace FarmHand.Domain.Entities;

public class LivestockCategory : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public string Name { get; protected set; } = string.Empty; // e.g. "Poultry", "Goats", "Cattle"
    public string? Description { get; protected set; }
    /// <summary>Head count maintained by the farmer for flock-level groups (individual animals are counted separately).</summary>
    public int AnimalCount { get; protected set; }
    public string? Species { get; protected set; }

    protected LivestockCategory() { }

    public LivestockCategory(Guid farmId, string name, string? description = null, string? species = null, int headcount = 0)
    {
        FarmId = farmId;
        Name = name;
        Description = description;
        Species = species;
        AnimalCount = Math.Max(0, headcount);
    }

    public void UpdateDetails(string name, string? description = null, string? species = null, int? headcount = null)
    {
        Name = name;
        Description = description;
        Species = species;
        if (headcount is { } count) AnimalCount = Math.Max(0, count);
        MarkUpdated();
    }

    public void SetHeadcount(int headcount)
    {
        AnimalCount = Math.Max(0, headcount);
        MarkUpdated();
    }

    public void IncrementAnimalCount() => AnimalCount++;
    public void DecrementAnimalCount() => AnimalCount = Math.Max(0, AnimalCount - 1);
}
