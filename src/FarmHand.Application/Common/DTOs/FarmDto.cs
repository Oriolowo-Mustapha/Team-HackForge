namespace FarmHand.Application.Common.DTOs;

/// <summary>Farm summary returned by dashboard endpoints.</summary>
/// <param name="Id">Farm id.</param>
/// <param name="OwnerId">Identifier of the authenticated user who created the farm.</param>
/// <param name="DisplayName">Human-readable farm name.</param>
/// <param name="Location">Free-text location.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public record FarmDto(
    Guid Id,
    string OwnerId,
    string DisplayName,
    string Location,
    DateTime CreatedAt);
