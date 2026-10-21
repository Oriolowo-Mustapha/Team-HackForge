namespace FarmHand.Application.Common.DTOs;

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
