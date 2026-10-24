using System.Text.Json;
using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;

namespace FarmHand.Application.Common.Services;

/// <summary>Stored JSON shape for <see cref="PoultryObservation.ExtractionDetails"/>.</summary>
public record ExtractionDetails(List<string> Symptoms, List<string> MissingInformation);

/// <summary>Stored JSON shape for <see cref="PoultryObservation.VisionAnalysis"/>.</summary>
public record StoredVision(string? Description, string[]? DetectedIssues, decimal? Confidence, string? Source, bool? Available, DateTime? AnalyzedAt);

public static class Mappers
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static IncidentDto ToDto(this Incident i) => new(
        i.Id, i.FarmId, i.LivestockAnimalId, i.LivestockCategoryId, i.DetectedAt, i.IncidentType, i.Severity,
        i.TriggeringObservations, i.EvidenceSummary, i.Status, i.RuleVersion, i.FollowUpDate, i.ResolvedAt);

    public static VetSummaryDto ToSummary(this VetPartner v) =>
        new(v.Id, v.Name, v.Specialization, v.Contact, v.HasPayoutAccount);

    public static PaymentDto ToDto(this Payment p, bool includePayout, string? farmName = null, Guid? incidentId = null) => new(
        p.Id,
        p.VetRequestId,
        p.Status,
        p.Mode,
        p.Amount.Amount,
        p.PlatformCommission.Amount,
        p.TotalAmount.Amount,
        includePayout ? p.VetPayoutAmount.Amount : null,
        p.TotalAmount.Currency,
        p.TransactionReference,
        p.Status == PaymentStatus.Pending ? p.CheckoutUrl : null,
        p.VetSubaccountUsed,
        p.FailureReason,
        p.CreatedAt,
        p.PaidAt,
        farmName,
        incidentId);

    public static string SerializeExtraction(ObservationExtraction extraction) =>
        JsonSerializer.Serialize(new ExtractionDetails(extraction.Symptoms.ToList(), extraction.MissingInformation.ToList()));

    public static ExtractionDetails ReadExtraction(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ExtractionDetails([], []);
        try
        {
            var parsed = JsonSerializer.Deserialize<ExtractionDetails>(json, Json);
            if (parsed is null) return new ExtractionDetails([], []);
            var approvedQuestions = RuleBasedAnalyzer.Extract(string.Empty).MissingInformation;
            return new ExtractionDetails(RuleBasedAnalyzer.ApprovedSymptoms(parsed.Symptoms).ToList(),
                (parsed.MissingInformation ?? []).Where(q => approvedQuestions.Contains(q)).Take(4).ToList());
        }
        catch (JsonException)
        {
            return new ExtractionDetails([], []);
        }
    }

    public static string SerializeVision(VisionAnalysisResult vision) => JsonSerializer.Serialize(new StoredVision(
        vision.Description, vision.DetectedIssues, vision.Confidence, vision.Source, vision.Available, DateTime.UtcNow));

    public static VisionDto? ReadVision(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var v = JsonSerializer.Deserialize<StoredVision>(json, Json);
            if (v is null) return null;
            var safe = PhotoSafety.Constrain(new VisionAnalysisResult(string.Empty, v.DetectedIssues ?? [],
                v.Confidence, v.Source ?? "unknown", v.Available ?? true));
            return new VisionDto(safe.Description, safe.DetectedIssues, safe.Confidence, safe.Source, safe.Available);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsAiSource(string? source) =>
        source is not null && (source.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) || source.StartsWith("groq", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("Gemini/Groq", StringComparison.OrdinalIgnoreCase));

    public static string SerializeFarmerActions(IReadOnlyList<string> actions) =>
        JsonSerializer.Serialize(actions ?? []);

    public static IReadOnlyList<string> ReadFarmerActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static AssessmentDto ToDto(this AIDiagnosis d, PoultryObservation observation, int? headcount = null, Severity? linkedCaseSeverity = null)
    {
        // Historical model prose is never rendered as current advice. Preserve the stored record
        // and derive safe display guidance without writing back to the database.
        var safe = AssessmentSafety.Apply(AssessmentSafety.InputFor(observation),
            new AIAssessment(AssessmentSafety.StoredSeverity(d.Severity, d.RequiresVetConsultation, linkedCaseSeverity),
                string.Empty, string.Empty, false, null, d.Source ?? "rules"), headcount);
        return new AssessmentDto(safe.Severity, safe.DiagnosisSummary, safe.RecommendedAction,
            safe.FarmerActions ?? [], safe.RequiresVetConsultation, d.Source, IsAiSource(d.Source), d.CreatedAt);
    }

    public static string SubjectName(LivestockAnimal? animal, LivestockCategory? category) =>
        animal is not null ? animal.Name : category?.Name ?? "Unspecified";
}
