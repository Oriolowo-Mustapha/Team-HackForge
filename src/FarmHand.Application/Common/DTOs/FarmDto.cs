namespace FarmHand.Application.Common.DTOs;

public record FarmDto(
    Guid Id,
    string OwnerId,
    string DisplayName,
    string Location,
    DateTime CreatedAt);
