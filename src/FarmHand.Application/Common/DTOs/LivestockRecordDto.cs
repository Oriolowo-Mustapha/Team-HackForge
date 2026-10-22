namespace FarmHand.Application.Common.DTOs;

/// <summary>Livestock record (animal) summary returned by the livestock/records endpoint.</summary>
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
/// <param name="DiseaseHistory">Past illnesses and conditions.</param>
/// <param name="RecentGestationDate">Most recent gestation/birthing date.</param>
/// <param name="LastVeterinaryVisit">Last vet visit date.</param>
/// <param name="Vaccinations">Comma-separated vaccination records.</param>
/// <param name="LastLactationDate">Last lactation date (for dairy animals).</param>
/// <param name="Parentage">Sire/dam or breeding line info.</param>
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
    DateTime CreatedAt,
    string? DiseaseHistory = null,
    DateTime? RecentGestationDate = null,
    DateTime? LastVeterinaryVisit = null,
    string? Vaccinations = null,
    DateTime? LastLactationDate = null,
    string? Parentage = null);
