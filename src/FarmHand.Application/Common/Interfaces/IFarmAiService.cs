namespace FarmHand.Application.Common.Interfaces;

/// <summary>Structured fields extracted from a farmer's report.</summary>
public record ExtractedObservation(
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes);

/// <summary>
/// Result of turning free text into structured observation fields, including which engine produced it.
/// <see cref="Source"/> is "gemini:&lt;model&gt;", "groq:&lt;model&gt;", "rules" (explicitly enabled demo fallback) or "manual".
/// <see cref="DetectedLanguage"/> is the report language as a canonical code ("yo", "ha", "ig", "pcm", "en") when known.
/// </summary>
public record ObservationExtraction(
    ExtractedObservation Fields,
    IReadOnlyList<string> Symptoms,
    IReadOnlyList<string> MissingInformation,
    string Source,
    string? DetectedLanguage = null)
{
    public bool IsAi => Source.StartsWith("gemini:", StringComparison.Ordinal) || Source.StartsWith("groq:", StringComparison.Ordinal);
}

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

/// <summary>Severity assessment. <see cref="Source"/> is the AI model or "rules" when the rule-based triage was used.
/// <see cref="FarmerActions"/> lists practical care steps the farmer can do at the farm level without a vet.</summary>
public record AIAssessment(
    FarmHand.Domain.Enums.Severity Severity,
    string DiagnosisSummary,
    string RecommendedAction,
    bool RequiresVetConsultation,
    string? RawAssessment,
    string Source = "rules",
    IReadOnlyList<string>? FarmerActions = null);

/// <summary>Everything the assessment can use about the observation being confirmed.</summary>
public record AssessmentInput(
    string? Transcript,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    string? VisionAnalysisJson,
    string? SubjectDescription,
    FarmHistoryContext? History,
    string? ReportLanguage = null);

public interface IFarmAiService
{
    /// <summary>True when at least one AI text provider has credentials configured.</summary>
    bool IsAiConfigured { get; }

    /// <summary>
    /// Extracts structured fields from free text. Throws <see cref="Exceptions.AiUnavailableException"/>
    /// when no provider succeeds and the labelled rule-based demo fallback is not enabled.
    /// </summary>
    Task<ObservationExtraction> ExtractObservationAsync(string rawText, string? subjectDescription, CancellationToken ct = default);

    /// <summary>
    /// Assesses severity. Never throws for provider failures: when no AI provider is available the
    /// deterministic rule-based triage is used and reported through <see cref="AIAssessment.Source"/>.
    /// </summary>
    Task<AIAssessment> AssessSeverityAsync(AssessmentInput input, CancellationToken ct = default);
}
