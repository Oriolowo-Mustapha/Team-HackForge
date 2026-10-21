namespace FarmHand.Application.Common.DTOs;

public record VetRequestDto(
    Guid Id,
    Guid IncidentId,
    Domain.Enums.RequestType RequestType,
    Domain.Enums.RequestStatus Status,
    decimal FarmerCost,
    DateTime? RespondedAt);
