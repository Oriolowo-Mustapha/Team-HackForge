using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;
using FarmHand.Infrastructure.AI.Clients;
using FarmHand.Infrastructure.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FarmHand.Infrastructure.AI.Services;

public class FarmAiService : IFarmAiService
{
    private readonly IGeminiAiClient _geminiClient;
    private readonly IGroqAiClient _groqClient;
    private readonly ILogger<FarmAiService> _logger;

    public FarmAiService(
        IGeminiAiClient geminiClient,
        IGroqAiClient groqClient,
        ILogger<FarmAiService> logger)
    {
        _geminiClient = geminiClient;
        _groqClient = groqClient;
        _logger = logger;
    }

    public async Task<ExtractedObservation> ExtractObservationAsync(string rawText, CancellationToken ct = default)
    {
        string? rawJson = null;

        try
        {
            var requestText = AIInputGuard.Sanitize(rawText);
            rawJson = await _geminiClient.ExtractObservationRawAsync(requestText, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Gemini failed, falling back to Groq");
            try
            {
                var requestText = AIInputGuard.Sanitize(rawText);
                rawJson = await _groqClient.ExtractObservationRawAsync(requestText, ct);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Both Gemini and Groq failed");
                throw new InvalidOperationException("AI extraction service unavailable", ex2);
            }
        }

        if (string.IsNullOrWhiteSpace(rawJson))
            throw new InvalidOperationException("AI returned empty response.");

        var cleaned = CleanJsonResponse(rawJson);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<AiObservationDto>(cleaned, options)
            ?? throw new InvalidOperationException("Failed to parse AI response.");

        return new ExtractedObservation(
            parsed.MortalityCount,
            parsed.WaterConsumption,
            parsed.FeedConsumption,
            parsed.EggProduction,
            parsed.FarmerNotes);
    }

    public async Task<AIAssessment> AssessSeverityAsync(
        string rawText,
        string? farmerNotes,
        string? visionAnalysisJson,
        FarmHistoryContext? history,
        CancellationToken ct = default)
    {
        var safeRawText = AIInputGuard.Sanitize(rawText);
        var safeFarmerNotes = AIInputGuard.Sanitize(farmerNotes);
        var safeVision = AIInputGuard.SanitizeFacts(visionAnalysisJson);
        var safeHistory = history is null
            ? null
            : new FarmHistoryContext(
                history.RecentObservations.Select(o => new RecentObservationSummary(
                    o.Id,
                    o.RecordedAt,
                    o.MortalityCount,
                    AIInputGuard.Sanitize(o.FarmerNotes),
                    o.ImageUrl)).ToList(),
                history.RecentIncidents.ToList(),
                history.LivestockHistory.ToList());

        var prompt = BuildAssessmentPrompt(safeRawText, safeFarmerNotes, safeVision, safeHistory);

        string? rawResponse = null;

        try
        {
            rawResponse = await _geminiClient.AssessSeverityRawAsync(prompt, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Gemini severity assessment failed, falling back to Groq");
            try
            {
                rawResponse = await _groqClient.AssessSeverityRawAsync(prompt, ct);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Both Gemini and Groq failed for severity assessment");
                throw new InvalidOperationException("AI assessment service unavailable", ex2);
            }
        }

        if (string.IsNullOrWhiteSpace(rawResponse))
            throw new InvalidOperationException("AI returned empty assessment response.");

        var cleaned = CleanJsonResponse(rawResponse);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<AiAssessmentDto>(cleaned, options)
            ?? throw new InvalidOperationException("Failed to parse AI assessment response.");

        var severity = ParseSeverity(parsed.Severity);
        var requiresVetConsultation = severity is Severity.High or Severity.Critical;

        return new AIAssessment(
            severity,
            parsed.DiagnosisSummary ?? string.Empty,
            parsed.RecommendedAction ?? string.Empty,
            requiresVetConsultation,
            rawResponse);
    }

    private static string BuildAssessmentPrompt(
        string rawText,
        string? farmerNotes,
        string? visionAnalysisJson,
        FarmHistoryContext? history)
    {
        var historySection = string.Empty;
        if (history is not null)
        {
            var recentObs = history.RecentObservations.Count > 0
                ? string.Join("\n", history.RecentObservations.Select(o =>
                    $"- {o.RecordedAt:yyyy-MM-dd}: mortality={o.MortalityCount}, notes={o.FarmerNotes ?? "(none)"}"))
                : "None";

            var recentInc = history.RecentIncidents.Count > 0
                ? string.Join("\n", history.RecentIncidents.Select(i =>
                    $"- {i.DetectedAt:yyyy-MM-dd}: {i.IncidentType} (severity={i.Severity}, status={i.Status})"))
                : "None";

            var livestock = history.LivestockHistory.Count > 0
                ? string.Join("\n", history.LivestockHistory.Select(l =>
                    $"- {l.Name} ({l.Species}): disease history={l.DiseaseHistory ?? "(none)"}, last vet visit={l.LastVeterinaryVisit?.ToString("yyyy-MM-dd") ?? "(never)"}, vaccinations={l.Vaccinations ?? "(none)"}, recent gestation={l.RecentGestationDate?.ToString("yyyy-MM-dd") ?? "(n/a)"}"))
                : "None";

            historySection = $@"
FARM HISTORY CONTEXT:
Recent observations:
{recentObs}

Recent incidents:
{recentInc}

Livestock history:
{livestock}";
        }

        var visionSection = string.IsNullOrWhiteSpace(visionAnalysisJson)
            ? string.Empty
            : $"\nVISION ANALYSIS:\n{visionAnalysisJson}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are an AI veterinary assistant for a farm management system. Analyze the following farmer observation and assess the severity level.");
        sb.AppendLine();
        sb.Append("Farmer observation: \"");
        sb.Append(rawText);
        sb.AppendLine("\"");
        sb.Append("Farmer notes: ");
        sb.AppendLine(farmerNotes ?? "(none)");
        if (!string.IsNullOrWhiteSpace(visionSection))
        {
            sb.Append(visionSection);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(historySection))
        {
            sb.Append(historySection);
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("Return JSON only with these fields:");
        sb.AppendLine("- severity: one of 'Low', 'Medium', 'High', 'Critical'");
        sb.AppendLine("- diagnosisSummary: brief summary of the likely condition");
        sb.AppendLine("- recommendedAction: what the farmer should do next");
        sb.AppendLine("- requiresVetConsultation: true if severity is High or Critical, otherwise false");
        sb.AppendLine();
        sb.AppendLine("Consider the farm history context when assessing severity. If there are recent similar incidents or concerning patterns in the livestock history, escalate the severity accordingly.");
        sb.AppendLine();
        sb.AppendLine("Do not include any text outside the JSON.");

        return sb.ToString();
    }

    private static Severity ParseSeverity(string? severityStr)
    {
        if (string.IsNullOrWhiteSpace(severityStr))
            return Severity.Low;

        return severityStr.Trim() switch
        {
            "Critical" => Severity.Critical,
            "High" => Severity.High,
            "Medium" => Severity.Medium,
            _ => Severity.Low
        };
    }

    private static string CleanJsonResponse(string raw)
    {
        var match = Regex.Match(raw, @"\{.*\}", RegexOptions.Singleline);
        return match.Success ? match.Value : raw;
    }

    private class AiObservationDto
    {
        public int MortalityCount { get; set; }
        public string? WaterConsumption { get; set; }
        public string? FeedConsumption { get; set; }
        public string? EggProduction { get; set; }
        public string? FarmerNotes { get; set; }
    }

    private class AiAssessmentDto
    {
        public string? Severity { get; set; }
        public string? DiagnosisSummary { get; set; }
        public string? RecommendedAction { get; set; }
        public bool RequiresVetConsultation { get; set; }
    }
}
