using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using System.Text.Json;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Default rule engine for auto-detecting incidents from poultry observations.</summary>
public class PoultryRuleEngine : IPoultryRuleEngine
{
    private static readonly int[] MortalitySeverityThresholds = { 5, 10, 25, 50 };

    public Task<IReadOnlyList<RuleResult>> EvaluateAsync(
        int mortalityCount,
        string? farmerNotes,
        string? visionAnalysisJson,
        CancellationToken ct = default)
    {
        var results = new List<RuleResult>();

        // Rule 1: Any mortality triggers an incident.
        if (mortalityCount > 0)
        {
            var severity = SeverityForMortality(mortalityCount);
            results.Add(new RuleResult(
                true,
                "Poultry Mortality",
                ((FarmHand.Domain.Enums.Severity)severity).ToString(),
                $"{mortalityCount} mortality{(mortalityCount == 1 ? "" : "ies")} reported."));
        }

        // Rule 2: Farmer notes contain concerning keywords.
        if (!string.IsNullOrWhiteSpace(farmerNotes) && ContainsConcerningKeywords(farmerNotes))
        {
            results.Add(new RuleResult(
                true,
                "Farmer Reported Concern",
                "Medium",
                "Farmer notes indicate a potential health concern."));
        }

        // Rule 3: Vision analysis detected issues.
        if (!string.IsNullOrWhiteSpace(visionAnalysisJson) && VisionAnalysisHasIssues(visionAnalysisJson))
        {
            results.Add(new RuleResult(
                true,
                "Vision Detected Issue",
                "Medium",
                "Image analysis detected potential issues."));
        }

        return Task.FromResult<IReadOnlyList<RuleResult>>(results);
    }

    private static FarmHand.Domain.Enums.Severity SeverityForMortality(int count)
    {
        for (var i = 0; i < MortalitySeverityThresholds.Length; i++)
        {
            if (count < MortalitySeverityThresholds[i])
                return (FarmHand.Domain.Enums.Severity)i;
        }
        return FarmHand.Domain.Enums.Severity.Critical;
    }

    private static bool ContainsConcerningKeywords(string notes)
    {
        var lower = notes.ToLowerInvariant();
        return lower.Contains("dead") || lower.Contains("dying")
            || lower.Contains("sick") || lower.Contains("ill")
            || lower.Contains("mortality") || lower.Contains("loss");
    }

    private static bool VisionAnalysisHasIssues(string visionAnalysisJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(visionAnalysisJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("DetectedIssues", out var issues) && issues.ValueKind == JsonValueKind.Array)
            {
                return issues.GetArrayLength() > 0;
            }

            if (root.TryGetProperty("Confidence", out var confidence) && confidence.TryGetDouble(out var conf))
            {
                return conf > 0.7;
            }
        }
        catch
        {
            // Malformed vision JSON is treated as no issues.
        }

        return false;
    }
}
