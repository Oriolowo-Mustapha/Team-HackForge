using FarmHand.Application.Common.Interfaces;

namespace FarmHand.Application.Common.Services;

/// <summary>Only approved concern labels and fixed uncertainty text may leave photo analysis.</summary>
public static class PhotoSafety
{
    public static readonly IReadOnlyDictionary<string, string> Concerns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["possible_dead_animals"] = "Possible dead animals",
        ["breathing_concern"] = "Laboured or noisy breathing",
        ["discharge"] = "Nasal or eye discharge",
        ["visible_wound"] = "Skin lesions or sores",
        ["abnormal_droppings"] = "Abnormal droppings or diarrhoea",
        ["blood_in_droppings"] = "Blood in droppings",
        ["wet_litter"] = "Wet litter or bedding",
        ["dirty_feed_or_water"] = "Feed or water hygiene concern",
        ["crowding"] = "Possible crowding",
        ["swelling"] = "Swelling",
        ["bloating"] = "Bloated belly"
    };

    public static VisionAnalysisResult Constrain(VisionAnalysisResult result)
    {
        if (!result.Available)
            return VisionAnalysisResult.Unavailable("Photo assistance is unavailable. Describe the signs in your own words or request a veterinarian.");
        var issues = result.DetectedIssues.Where(issue => !string.IsNullOrWhiteSpace(issue))
            .SelectMany(issue => Concerns.TryGetValue(issue.Trim(), out var label) ? new[] { label }
                : Concerns.Values.Where(v => string.Equals(v, issue.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Concat(RuleBasedAnalyzer.DetectSymptoms(issue).Select(s => s.Label)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray();
        var description = issues.Length > 0
            ? "Photo assistance flagged possible visible concerns for farmer review. A photo cannot confirm a diagnosis."
            : "No approved visible concern was identified. Photo assistance is uncertain and cannot rule out illness or establish a diagnosis.";
        return result with { Description = description, DetectedIssues = issues,
            Confidence = result.Confidence is { } confidence ? Math.Clamp(confidence, 0m, 1m) : null };
    }
}
