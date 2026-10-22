namespace FarmHand.Application.Common.DTOs;

/// <summary>Incident action summary.</summary>
/// <param name="Id">Action id.</param>
/// <param name="IncidentId">Related incident id.</param>
/// <param name="Description">What action to take.</param>
/// <param name="Priority">Action priority.</param>
/// <param name="Status">Current status.</param>
/// <param name="CompletedAt">UTC completion timestamp, if any.</param>
/// <param name="CompletionNotes">Optional completion notes.</param>
public record IncidentActionDto(
    Guid Id,
    Guid IncidentId,
    string Description,
    string Priority,
    string Status,
    DateTime? CompletedAt,
    string? CompletionNotes);
