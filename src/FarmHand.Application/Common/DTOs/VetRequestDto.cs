namespace FarmHand.Application.Common.DTOs;

/// <summary>Vet request summary.</summary>
/// <param name="Id">Vet request id.</param>
/// <param name="IncidentId">Related incident id.</param>
/// <param name="RequestType">Coordination model (serialized as an integer).</param>
/// <param name="Status">Current request status (serialized as an integer).</param>
/// <param name="FarmerCost">Farmer's share of the cost, in NGN.</param>
/// <param name="RespondedAt">UTC timestamp when a vet responded.</param>
public record VetRequestDto(
    Guid Id,
    Guid IncidentId,
    Domain.Enums.RequestType RequestType,
    Domain.Enums.RequestStatus Status,
    decimal FarmerCost,
    DateTime? RespondedAt);
