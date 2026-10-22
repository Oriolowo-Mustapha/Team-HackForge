namespace FarmHand.Application.Common.DTOs;

/// <summary>Livestock record (animal) summary returned by the goats/records endpoint.</summary>
/// <param name="Id">Animal id.</param>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="CategoryId">Livestock category id.</param>
/// <param name="Name">Animal name or tag.</param>
/// <param name="Species">Species, for example "goat".</param>
/// <param name="Breed">Animal breed, if known.</param>
/// <param name="AgeMonths">Approximate age in months.</param>
/// <param name="Gender">Animal gender.</param>
/// <param name="IsActive">Whether the animal is active.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public record LivestockRecordDto(
    Guid Id,
    Guid FarmId,
    Guid CategoryId,
    string Name,
    string Species,
    string? Breed,
    int? AgeMonths,
    string? Gender,
    bool IsActive,
    DateTime CreatedAt);
