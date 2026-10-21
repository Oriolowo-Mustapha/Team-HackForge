namespace FarmHand.Application.Common.DTOs;

/// <summary>Poultry observation summary. Not yet exposed by an endpoint.</summary>
/// <param name="Id">Observation id.</param>
/// <param name="FlockId">Flock id.</param>
/// <param name="RecordedAt">UTC recording timestamp.</param>
/// <param name="MortalityCount">Deaths reported for the period.</param>
/// <param name="WaterConsumption">Free-text water consumption.</param>
/// <param name="FeedConsumption">Free-text feed consumption.</param>
/// <param name="EggProduction">Free-text egg production.</param>
/// <param name="FarmerNotes">Free-text farmer notes.</param>
/// <param name="ConfirmationStatus">Farmer confirmation state (serialized as an integer).</param>
public record PoultryObservationDto(
    Guid Id,
    Guid FlockId,
    DateTime RecordedAt,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    Domain.Enums.ObservationConfirmationStatus ConfirmationStatus);
