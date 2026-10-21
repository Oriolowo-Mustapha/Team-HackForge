namespace FarmHand.Application.Common.DTOs;

public record IncidentDto(
    Guid Id,
    Guid FarmId,
    Guid FlockId,
    DateTime DetectedAt,
    string IncidentType,
    Domain.Enums.Severity Severity,
    string TriggeringObservations,
    string EvidenceSummary,
    Domain.Enums.IncidentStatus Status,
    string? RuleVersion,
    DateTime? FollowUpDate,
    DateTime? ResolvedAt);
