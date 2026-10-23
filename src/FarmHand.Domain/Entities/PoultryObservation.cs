namespace FarmHand.Domain.Entities;

/// <summary>
/// A farmer-reported health observation. Despite the historical name it covers both
/// flock-level reports (<see cref="LivestockCategoryId"/>) and individual-animal reports
/// (<see cref="LivestockAnimalId"/>).
/// </summary>
public class PoultryObservation : BaseEntity
{
    public Guid FarmId { get; protected set; }
    public Guid? LivestockAnimalId { get; protected set; }
    public Guid? LivestockCategoryId { get; protected set; } // for flock-level observations
    public DateTime RecordedAt { get; protected set; }
    public int MortalityCount { get; protected set; }
    public string? WaterConsumption { get; protected set; }
    public string? FeedConsumption { get; protected set; }
    public string? EggProduction { get; protected set; }
    public string? FarmerNotes { get; protected set; }
    public string? RawTranscript { get; protected set; }
    public string? ImageUrl { get; protected set; }
    public string? VisionAnalysis { get; protected set; }
    public ObservationConfirmationStatus ConfirmationStatus { get; protected set; }

    /// <summary>Where the structured fields came from, e.g. "gemini:gemini-3.5-flash", "rules", or "manual".</summary>
    public string? ExtractedByAi { get; protected set; }

    /// <summary>JSON with extracted symptoms and missing-information prompts.</summary>
    public string? ExtractionDetails { get; protected set; }
    public string? InputChannels { get; protected set; }
    public string? IdempotencyKey { get; protected set; }

    protected PoultryObservation() { }

    public PoultryObservation(
        Guid farmId,
        Guid? livestockAnimalId,
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
        FarmId = farmId;
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

    public bool IsPending => ConfirmationStatus == ObservationConfirmationStatus.Pending;

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

    /// <summary>
    /// Applies farmer corrections to the extracted fields of a pending observation.
    /// Returns true when any value changed.
    /// </summary>
    public bool ApplyCorrections(
        int? mortalityCount,
        string? waterConsumption,
        string? feedConsumption,
        string? eggProduction,
        string? farmerNotes)
    {
        if (!IsPending)
            throw new InvalidOperationException("Only a pending observation can be corrected.");

        var changed = false;
        if (mortalityCount is { } mortality && mortality != MortalityCount) { MortalityCount = mortality; changed = true; }
        if (waterConsumption is not null && waterConsumption != WaterConsumption) { WaterConsumption = Blank(waterConsumption); changed = true; }
        if (feedConsumption is not null && feedConsumption != FeedConsumption) { FeedConsumption = Blank(feedConsumption); changed = true; }
        if (eggProduction is not null && eggProduction != EggProduction) { EggProduction = Blank(eggProduction); changed = true; }
        if (farmerNotes is not null && farmerNotes != FarmerNotes) { FarmerNotes = Blank(farmerNotes); changed = true; }
        if (changed) MarkUpdated();
        return changed;
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Discard()
    {
        if (!IsPending)
            throw new InvalidOperationException("Only a pending observation can be discarded.");
        IdempotencyKey = null; // frees the key so the farmer can resubmit the same draft
        MarkDeleted();
        MarkUpdated();
    }

    public void MarkAiExtracted(string aiModel)
    {
        ExtractedByAi = aiModel;
        MarkUpdated();
    }

    public void SetExtractionDetails(string? detailsJson)
    {
        ExtractionDetails = detailsJson;
        MarkUpdated();
    }

    public void SetInputChannels(string channels)
    {
        InputChannels = channels;
    }

    public void SetIdempotencyKey(string key)
    {
        IdempotencyKey = key;
        MarkUpdated();
    }

    public void SetRawTranscript(string rawTranscript)
    {
        RawTranscript = rawTranscript;
        MarkUpdated();
    }

    public void SetImageAnalysis(string imageUrl, string visionAnalysis)
    {
        ImageUrl = imageUrl;
        VisionAnalysis = visionAnalysis;
        MarkUpdated();
    }
}
