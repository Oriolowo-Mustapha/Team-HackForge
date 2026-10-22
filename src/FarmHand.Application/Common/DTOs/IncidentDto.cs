namespace FarmHand.Application.Common.DTOs;

/// <summary>Incident summary.</summary>
/// <param name="Id">Incident id.</param>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="LivestockAnimalId">Affected animal id, when animal-level.</param>
/// <param name="LivestockCategoryId">Affected category id, when flock-level.</param>
/// <param name="DetectedAt">UTC detection timestamp.</param>
/// <param name="IncidentType">Short incident classification.</param>
/// <param name="Severity">Severity level (serialized as an integer).</param>
/// <param name="TriggeringObservations">Evidence that triggered the incident.</param>
/// <param name="EvidenceSummary">Summary shown to the farmer and vet.</param>
/// <param name="Status">Current lifecycle status (serialized as an integer).</param>
/// <param name="RuleVersion">Version of the detection rule that raised the incident.</param>
/// <param name="FollowUpDate">Optional scheduled follow-up date.</param>
/// <param name="ResolvedAt">UTC resolution timestamp, when resolved.</param>
public record IncidentDto(
    Guid Id,
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    DateTime DetectedAt,
    string IncidentType,
    Domain.Enums.Severity Severity,
    string TriggeringObservations,
    string EvidenceSummary,
    Domain.Enums.IncidentStatus Status,
    string? RuleVersion,
    DateTime? FollowUpDate,
    DateTime? ResolvedAt);
