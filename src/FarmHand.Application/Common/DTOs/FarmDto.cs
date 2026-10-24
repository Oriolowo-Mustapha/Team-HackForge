namespace FarmHand.Application.Common.DTOs;

/// <summary>Farm summary returned by dashboard endpoints.</summary>
/// <param name="Id">Farm id.</param>
/// <param name="OwnerId">Identifier of the authenticated user who created the farm.</param>
/// <param name="DisplayName">Human-readable farm name.</param>
/// <param name="Location">Free-text location.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="Latitude">Optional GPS latitude.</param>
/// <param name="Longitude">Optional GPS longitude.</param>
/// <param name="CityOrTown">Optional town or city.</param>
/// <param name="State">Optional state.</param>
/// <param name="Country">Country name.</param>
/// <param name="LocationSource">GPS or manual entry.</param>
public record FarmDto(
    Guid Id,
    string OwnerId,
    string DisplayName,
    string Location,
    DateTime CreatedAt,
    double? Latitude = null,
    double? Longitude = null,
    string? CityOrTown = null,
    string? State = null,
    string? Country = null,
    string? LocationSource = null);
