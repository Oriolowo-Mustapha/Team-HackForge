namespace FarmHand.Application.Common.DTOs;

/// <summary>Follow-up record summary.</summary>
/// <param name="Id">Follow-up id.</param>
/// <param name="IncidentId">Related incident id.</param>
/// <param name="FarmerResponse">Farmer-reported outcome.</param>
/// <param name="Notes">Optional follow-up notes.</param>
/// <param name="RecordedAt">UTC recording timestamp.</param>
public record FollowUpDto(
    Guid Id,
    Guid IncidentId,
    Domain.Enums.Outcome FarmerResponse,
    string? Notes,
    DateTime RecordedAt);
