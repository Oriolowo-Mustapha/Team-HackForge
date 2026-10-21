namespace FarmHand.Domain.Entities;

public class LivestockAnimal : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public Guid CategoryId { get; protected set; }
    public string Name { get; protected set; } = string.Empty; // e.g. "Adunni", "Hen #1"
    public string Species { get; protected set; } = string.Empty; // e.g. "Chicken", "Goat", "Cow"
    public string? Breed { get; protected set; }
    public int? AgeMonths { get; protected set; }
    public string? Gender { get; protected set; } // Male, Female
    public string? Notes { get; protected set; }
    public bool IsActive { get; protected set; } = true;

    protected LivestockAnimal() { }

    public LivestockAnimal(
        Guid farmId,
        Guid categoryId,
        string name,
        string species,
        string? breed = null,
        int? ageMonths = null,
        string? gender = null,
        string? notes = null)
    {
        FarmId = farmId;
        CategoryId = categoryId;
        Name = name;
        Species = species;
        Breed = breed;
        AgeMonths = ageMonths;
        Gender = gender;
        Notes = notes;
    }

    public void UpdateDetails(string name, string species, string? breed = null, int? ageMonths = null, string? gender = null, string? notes = null)
    {
        Name = name;
        Species = species;
        Breed = breed;
        AgeMonths = ageMonths;
        Gender = gender;
        Notes = notes;
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
