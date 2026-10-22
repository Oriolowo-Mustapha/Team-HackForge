using FarmHand.Domain.Enums;

namespace FarmHand.Application.Common.Interfaces;

public record ExtractedObservation(
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes);

public record FarmHistoryContext(
    List<RecentObservationSummary> RecentObservations,
    List<RecentIncidentSummary> RecentIncidents,
    List<LivestockHistorySummary> LivestockHistory);

public record RecentObservationSummary(
    Guid Id,
    DateTime RecordedAt,
    int MortalityCount,
    string? FarmerNotes,
    string? ImageUrl);

public record RecentIncidentSummary(
    Guid Id,
    DateTime DetectedAt,
    FarmHand.Domain.Enums.Severity Severity,
    string IncidentType,
    string Status);

public record LivestockHistorySummary(
    Guid AnimalId,
    string Name,
    string Species,
    string? DiseaseHistory,
    DateTime? RecentGestationDate,
    DateTime? LastVeterinaryVisit,
    string? Vaccinations);

public record AIAssessment(
    FarmHand.Domain.Enums.Severity Severity,
    string DiagnosisSummary,
    string RecommendedAction,
    bool RequiresVetConsultation,
    string? RawAssessment);

public interface IFarmAiService
{
    Task<ExtractedObservation> ExtractObservationAsync(string rawText, CancellationToken ct = default);
    Task<AIAssessment> AssessSeverityAsync(
        string rawText,
        string? farmerNotes,
        string? visionAnalysisJson,
        FarmHistoryContext? history,
        CancellationToken ct = default);
}
