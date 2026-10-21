namespace FarmHand.Domain.Entities;

public class PoultryObservation : BaseEntity
{
    public Guid LivestockAnimalId { get; protected set; }
    public Guid? LivestockCategoryId { get; protected set; } // for flock-level observations
    public DateTime RecordedAt { get; protected set; }
    public int MortalityCount { get; protected set; }
    public string? WaterConsumption { get; protected set; }
    public string? FeedConsumption { get; protected set; }
    public string? EggProduction { get; protected set; }
    public string? FarmerNotes { get; protected set; }
    public string? ImageUrl { get; protected set; }
    public string? VisionAnalysis { get; protected set; }
    public ObservationConfirmationStatus ConfirmationStatus { get; protected set; }
    public string? ExtractedByAi { get; protected set; }
    public string? IdempotencyKey { get; protected set; }

    protected PoultryObservation() { }

    public PoultryObservation(
        Guid livestockAnimalId,
        Guid? livestockCategoryId,
        DateTime recordedAt,
        int mortalityCount,
        string? waterConsumption,
        string? feedConsumption,
        string? eggProduction,
        string? farmerNotes,
        string? imageUrl = null,
        string? visionAnalysis = null)
    {
        LivestockAnimalId = livestockAnimalId;
        LivestockCategoryId = livestockCategoryId;
        RecordedAt = recordedAt;
        MortalityCount = mortalityCount;
        WaterConsumption = waterConsumption;
        FeedConsumption = feedConsumption;
        EggProduction = eggProduction;
        FarmerNotes = farmerNotes;
        ImageUrl = imageUrl;
        VisionAnalysis = visionAnalysis;
        ConfirmationStatus = ObservationConfirmationStatus.Pending;
    }

    public void Confirm(string? correctedNotes = null)
    {
        ConfirmationStatus = ObservationConfirmationStatus.Confirmed;
        if (!string.IsNullOrWhiteSpace(correctedNotes))
            FarmerNotes = correctedNotes;
        MarkUpdated();
    }

    public void Correct(string? correctedNotes = null)
    {
        ConfirmationStatus = ObservationConfirmationStatus.Corrected;
        if (!string.IsNullOrWhiteSpace(correctedNotes))
            FarmerNotes = correctedNotes;
        MarkUpdated();
    }

    public void MarkAiExtracted(string aiModel)
    {
        ExtractedByAi = aiModel;
        MarkUpdated();
    }

    public void SetIdempotencyKey(string key)
    {
        IdempotencyKey = key;
        MarkUpdated();
    }

    public void SetImageAnalysis(string imageUrl, string visionAnalysis)
    {
        ImageUrl = imageUrl;
        VisionAnalysis = visionAnalysis;
        MarkUpdated();
    }
}
