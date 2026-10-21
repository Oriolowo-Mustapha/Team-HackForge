namespace FarmHand.Application.Common.DTOs;

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
