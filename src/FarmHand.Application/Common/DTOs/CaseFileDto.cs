namespace FarmHand.Application.Common.DTOs;

/// <summary>Incident case file prepared for veterinary review. Not yet exposed by an endpoint.</summary>
/// <param name="IncidentId">Incident id.</param>
/// <param name="FarmName">Farm display name.</param>
/// <param name="FlockSize">Recorded flock population.</param>
/// <param name="DateOfConcern">Date the triggering observations were recorded.</param>
/// <param name="MortalityCount">Deaths reported that day.</param>
/// <param name="WaterConsumption">Reported water consumption.</param>
/// <param name="FeedConsumption">Reported feed consumption.</param>
/// <param name="EggProduction">Reported egg production.</param>
/// <param name="FarmerNotes">Free-text notes from the farmer.</param>
/// <param name="EvidenceSummary">Aggregated evidence summary.</param>
/// <param name="RecommendedActions">Suggested next actions.</param>
/// <param name="Disclaimer">Medical disclaimer shown alongside the case file.</param>
public record CaseFileDto(
    Guid IncidentId,
    string FarmName,
    int FlockSize,
    DateTime DateOfConcern,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    string EvidenceSummary,
    List<string> RecommendedActions,
    string Disclaimer);
