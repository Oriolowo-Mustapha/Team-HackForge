using System.Globalization;
using System.Text.RegularExpressions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;

namespace FarmHand.Application.Common.Services;

/// <summary>
/// Deterministic keyword and number rules used when no AI provider is reachable.
/// Everything it produces is labelled with source "rules" so the UI never presents it as AI output.
/// It is deliberately conservative: it extracts what the farmer literally said and never names a disease.
/// </summary>
public static partial class RuleBasedAnalyzer
{
    public const string Source = "rules";

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = 1, ["an"] = 1, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11,
        ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16,
        ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fifty"] = 50, ["hundred"] = 100
    };

    /// <summary>Symptom label, the patterns that indicate it, and whether it is a red-flag sign.</summary>
    private static readonly (string Label, Regex Pattern, bool RedFlag)[] SymptomRules =
    [
        ("Coughing", Rx(@"\bcough(s|ing|ed)?\b"), false),
        ("Sneezing", Rx(@"\bsneez(e|es|ing)\b"), false),
        ("Laboured or noisy breathing", Rx(@"\b(gasp(s|ing)?|breathing (hard|heavily|difficult)|difficult(y)? breathing|wheez(e|ing)|rattl(e|ing))\b"), true),
        ("Nasal or eye discharge", Rx(@"\b(discharge|runny nose|watery eyes?|mucus|snot)\b"), false),
        ("Abnormal droppings or diarrhoea", Rx(@"\b(diarrh(o)?ea|watery (droppings|poo|faeces|feces|stool)|green(ish)? (droppings|poo)|bloody (droppings|stool|poo)|loose (droppings|stool))\b"), false),
        ("Blood in droppings", Rx(@"\bbloody (droppings|stool|poo)|blood in (droppings|stool|poo)\b"), true),
        ("Reduced appetite", Rx(@"\b(not eating|off (their |its )?feed|stopped eating|won'?t eat|refus(e|es|ing) (to eat|feed)|reduced appetite|eating less|less feed)\b"), false),
        ("Reduced drinking", Rx(@"\b(not drinking|drinking less|stopped drinking|less water)\b"), false),
        ("Weakness or lethargy", Rx(@"\b(weak|lethargic|dull|sleepy|droopy|depressed|not active|inactive|can'?t stand|cannot stand)\b"), false),
        ("Twisted neck or loss of balance", Rx(@"\b(twisted neck|torticollis|walking in circles|loss of balance|paralys(is|ed)|tremor(s)?)\b"), true),
        ("Swelling", Rx(@"\b(swollen|swelling)\b"), false),
        ("Lameness or limping", Rx(@"\b(lame|lameness|limp(s|ing)?)\b"), false),
        ("Bloated belly", Rx(@"\bbloat(ed|ing)?\b"), true),
        ("Fever", Rx(@"\b(fever|feverish|very hot)\b"), false),
        ("Drop in egg production", Rx(@"\b(fewer eggs|egg(s)? (dropped|reduced|down)|drop in eggs?|less eggs|no eggs|egg production (dropped|reduced|fell))\b"), false),
        ("Ruffled feathers", Rx(@"\b(ruffled|rough) feathers\b"), false),
        ("Skin lesions or sores", Rx(@"\b(sores?|lesions?|scabs?|wounds?|blisters?)\b"), false),
        ("Difficulty giving birth", Rx(@"\b(difficult (birth|kidding|lambing|calving)|stuck kid|dystocia)\b"), true),
        ("Udder problems", Rx(@"\b(mastitis|hard udder|swollen udder|bloody milk)\b"), false),
        ("Sudden deaths", Rx(@"\b(sudden(ly)? (death|died|dead)|dropp(ed|ing) dead|died suddenly)\b"), true)
    ];

    private static Regex Rx(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\b(?<num>\d{1,5}|a|an|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|hundred)\s+(?:\w+\s+){0,3}?(?:died|dead|deaths?|mortalit(?:y|ies)|lost|passed away|perished)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MortalityBeforePattern();

    [GeneratedRegex(@"\b(?:lost|died|dead|deaths?|mortality(?: of| was| is)?)\s*(?:of\s+)?(?<num>\d{1,5}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|twenty|thirty|forty|fifty)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MortalityAfterPattern();

    [GeneratedRegex(@"\b(no (birds|animals|hens|goats)?\s*(have )?died|no deaths|no mortality|none (have )?died|nothing died)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoMortalityPattern();

    [GeneratedRegex(@"[^.!?\n]*\b(feed|feeding|eat|eating|appetite)\b[^.!?\n]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeedSentence();

    [GeneratedRegex(@"[^.!?\n]*\b(water|drink|drinking)\b[^.!?\n]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WaterSentence();

    [GeneratedRegex(@"[^.!?\n]*\beggs?\b[^.!?\n]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EggSentence();

    [GeneratedRegex(@"\b(since|yesterday|today|this morning|last night|days?|weeks?|hours?|started|began)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimingPattern();

    [GeneratedRegex(@"\b(\d{1,5}|all|most|many|several|some|few|half)\b[^.!?\n]{0,30}\b(birds?|hens?|chickens?|goats?|sheep|cows?|cattle|animals?|kids?|lambs?|pigs?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AffectedCountPattern();

    public static ObservationExtraction Extract(string? rawText)
    {
        var text = (rawText ?? string.Empty).Trim();
        var mortality = ExtractMortality(text);
        var symptoms = DetectSymptoms(text).Select(s => s.Label).ToList();

        var feed = FirstSentence(FeedSentence(), text);
        var water = FirstSentence(WaterSentence(), text);
        var eggs = FirstSentence(EggSentence(), text);

        var missing = new List<string>();
        if (mortality is null) missing.Add("Have any animals died? If so, how many and since when?");
        if (!AffectedCountPattern().IsMatch(text)) missing.Add("How many animals are showing these signs?");
        if (!TimingPattern().IsMatch(text)) missing.Add("When did you first notice the problem?");
        if (feed is null && water is null) missing.Add("Are the animals eating and drinking normally?");

        var fields = new ExtractedObservation(
            Math.Clamp(mortality ?? 0, 0, 10000),
            Truncate(water, 100),
            Truncate(feed, 100),
            Truncate(eggs, 100),
            Truncate(text, 1000));

        return new ObservationExtraction(fields, symptoms, missing, Source);
    }

    public static IReadOnlyList<(string Label, bool RedFlag)> DetectSymptoms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return SymptomRules.Where(r => r.Pattern.IsMatch(text)).Select(r => (r.Label, r.RedFlag)).ToList();
    }

    public static int? ExtractMortality(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (NoMortalityPattern().IsMatch(text)) return 0;

        var match = MortalityBeforePattern().Match(text);
        if (!match.Success) match = MortalityAfterPattern().Match(text);
        if (!match.Success) return null;

        var raw = match.Groups["num"].Value;
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
        return NumberWords.TryGetValue(raw, out var word) ? word : null;
    }

    /// <summary>
    /// Conservative rule-based triage: mortality thresholds (relative to flock size when known),
    /// red-flag signs, and vision-flagged issues. It never names a disease.
    /// </summary>
    public static AIAssessment Assess(AssessmentInput input, int? headcount = null)
    {
        var combined = string.Join(". ", new[] { input.Transcript, input.FarmerNotes }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var symptoms = DetectSymptoms(combined);
        var redFlags = symptoms.Where(s => s.RedFlag).Select(s => s.Label).ToList();
        var reasons = new List<string>();

        var severity = Severity.Low;
        if (input.MortalityCount > 0)
        {
            var mortalitySeverity = input.MortalityCount switch
            {
                >= 25 => Severity.Critical,
                >= 10 => Severity.High,
                >= 3 => Severity.Medium,
                _ => Severity.Low
            };

            if (headcount is > 0)
            {
                var rate = input.MortalityCount / (double)headcount.Value;
                var rateSeverity = rate switch
                {
                    >= 0.05 => Severity.Critical,
                    >= 0.02 => Severity.High,
                    >= 0.005 => Severity.Medium,
                    _ => Severity.Low
                };
                if (rateSeverity > mortalitySeverity) mortalitySeverity = rateSeverity;
                reasons.Add($"{input.MortalityCount} death{(input.MortalityCount == 1 ? "" : "s")} reported ({rate:P1} of {headcount} recorded animals)");
            }
            else
            {
                reasons.Add($"{input.MortalityCount} death{(input.MortalityCount == 1 ? "" : "s")} reported");
            }

            severity = mortalitySeverity;
        }

        if (redFlags.Count > 0)
        {
            severity = Max(severity, Severity.High);
            reasons.Add($"warning signs: {string.Join(", ", redFlags).ToLowerInvariant()}");
        }
        else if (symptoms.Count >= 3)
        {
            severity = Max(severity, Severity.Medium);
        }

        if (symptoms.Count > 0 && redFlags.Count == 0)
            reasons.Add($"signs reported: {string.Join(", ", symptoms.Select(s => s.Label)).ToLowerInvariant()}");

        if (!string.IsNullOrWhiteSpace(input.VisionAnalysisJson) && input.VisionAnalysisJson.Contains("\"DetectedIssues\":[\"", StringComparison.Ordinal))
        {
            severity = Max(severity, Severity.Medium);
            reasons.Add("the photo analysis flagged possible issues");
        }

        if (reasons.Count == 0)
            reasons.Add("no deaths or warning signs were identified in the report");

        var summary = $"Rule-based triage (no AI model was used): {string.Join("; ", reasons)}.";
        var action = severity switch
        {
            Severity.Critical => "Isolate affected animals now, stop movement on and off the farm, and contact a veterinarian today.",
            Severity.High => "Separate sick animals, keep records of deaths and signs, and request a veterinary review as soon as possible.",
            Severity.Medium => "Monitor closely over the next 24–48 hours, check feed and water, and request a vet if signs spread or deaths occur.",
            _ => "Keep observing and record any change. Report again if animals stop eating or deaths occur."
        };

        // Practical care steps the farmer can do at farm level; the vet is already covered by the action above.
        var farmerActions = severity switch
        {
            Severity.Critical => new[]
            {
                "Separate sick or dying animals from the healthy ones immediately",
                "Stop moving animals, people, tools and feed items on and off the farm",
                "Provide clean water and easy-to-eat feed; do not medicate on your own",
                "Record every death and new sign (time, number, what you saw)",
                "Prepare a clear path and light for the vet's arrival"
            },
            Severity.High => new[]
            {
                "Separate animals showing signs from the healthy flock",
                "Deepen biosecurity: disinfect boots, equipment and coops in use",
                "Provide fresh water and check feed and water intake twice a day",
                "Record deaths and signs so the vet can see the trend"
            },
            Severity.Medium => new[]
            {
                "Watch the animals closely for the next 24–48 hours",
                "Check feed and water consumption daily",
                "Improve housing hygiene and ventilation",
                "Report again immediately if more animals show signs or die"
            },
            _ => new[]
            {
                "Keep observing the animals and note any changes",
                "Ensure feed and water stay clean and available",
                "Report again if animals stop eating or any deaths occur"
            }
        };

        return new AIAssessment(
            severity,
            summary,
            action,
            severity is Severity.High or Severity.Critical,
            null,
            Source,
            farmerActions);
    }

    private static Severity Max(Severity a, Severity b) => a >= b ? a : b;

    private static string? FirstSentence(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
    }
}
