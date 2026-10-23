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
    public string? DiseaseHistory { get; protected set; } // Past illnesses and conditions
    public DateTime? RecentGestationDate { get; protected set; } // Most recent gestation/birthing date
    public DateTime? LastVeterinaryVisit { get; protected set; } // Last vet visit date
    public string? Vaccinations { get; protected set; } // Comma-separated vaccination records
    public DateTime? LastLactationDate { get; protected set; } // Last lactation date (for dairy animals)
    public string? Parentage { get; protected set; } // Sire/dam or breeding line info
    public bool IsActive { get; protected set; } = true;
    public string? PhotoUrl { get; protected set; }

    protected LivestockAnimal() { }

    public LivestockAnimal(
        Guid farmId,
        Guid categoryId,
        string name,
        string species,
        string? breed = null,
        int? ageMonths = null,
        string? gender = null,
        string? notes = null,
        string? diseaseHistory = null,
        DateTime? recentGestationDate = null,
        DateTime? lastVeterinaryVisit = null,
        string? vaccinations = null,
        DateTime? lastLactationDate = null,
        string? parentage = null)
    {
        FarmId = farmId;
        CategoryId = categoryId;
        Name = name;
        Species = species;
        Breed = breed;
        AgeMonths = ageMonths;
        Gender = gender;
        Notes = notes;
        DiseaseHistory = diseaseHistory;
        RecentGestationDate = recentGestationDate;
        LastVeterinaryVisit = lastVeterinaryVisit;
        Vaccinations = vaccinations;
        LastLactationDate = lastLactationDate;
        Parentage = parentage;
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

    public void UpdateHistory(
        string? diseaseHistory = null,
        DateTime? recentGestationDate = null,
        DateTime? lastVeterinaryVisit = null,
        string? vaccinations = null,
        DateTime? lastLactationDate = null,
        string? parentage = null)
    {
        DiseaseHistory = diseaseHistory;
        RecentGestationDate = recentGestationDate;
        LastVeterinaryVisit = lastVeterinaryVisit;
        Vaccinations = vaccinations;
        LastLactationDate = lastLactationDate;
        Parentage = parentage;
        MarkUpdated();
    }

    public void SetPhoto(string? photoUrl)
    {
        PhotoUrl = photoUrl;
        MarkUpdated();
    }

    public void MoveToCategory(Guid categoryId)
    {
        CategoryId = categoryId;
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
