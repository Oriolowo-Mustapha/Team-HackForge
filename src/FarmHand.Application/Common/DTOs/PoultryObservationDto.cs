namespace FarmHand.Application.Common.DTOs;

/// <summary>Poultry observation summary. Not yet exposed by a dedicated endpoint.</summary>
/// <param name="Id">Observation id.</param>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="LivestockAnimalId">Animal id, when animal-level.</param>
/// <param name="LivestockCategoryId">Category id, when flock-level.</param>
/// <param name="RecordedAt">UTC recording timestamp.</param>
/// <param name="MortalityCount">Deaths reported for the period.</param>
/// <param name="WaterConsumption">Free-text water consumption.</param>
/// <param name="FeedConsumption">Free-text feed consumption.</param>
/// <param name="EggProduction">Free-text egg production.</param>
/// <param name="FarmerNotes">Free-text farmer notes.</param>
/// <param name="ConfirmationStatus">Farmer confirmation state (serialized as an integer).</param>
public record PoultryObservationDto(
    Guid Id,
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    DateTime RecordedAt,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    Domain.Enums.ObservationConfirmationStatus ConfirmationStatus);
