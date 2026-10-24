using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Entities;

namespace FarmHand.Application.Common.Services;

/// <summary>
/// Model text is untrusted output. Only urgency can influence triage; all displayed care advice
/// comes from the approved supportive-care templates. Rules are conservative, not clinically validated.
/// </summary>
public static class AssessmentSafety
{
    public static AssessmentInput InputFor(PoultryObservation observation, string? subject = null, FarmHistoryContext? history = null)
    {
        var symptoms = RuleBasedAnalyzer.ApprovedSymptoms(Mappers.ReadExtraction(observation.ExtractionDetails).Symptoms);
        var notes = string.Join(". ", new[] { observation.FarmerNotes, string.Join(". ", symptoms) }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return new AssessmentInput(observation.RawTranscript, observation.MortalityCount, observation.WaterConsumption,
            observation.FeedConsumption, observation.EggProduction, notes, observation.VisionAnalysis, subject,
            history, observation.DetectedLanguage);
    }

    public static AIAssessment Apply(AssessmentInput input, AIAssessment assessment, int? headcount = null)
    {
        var rules = RuleBasedAnalyzer.Assess(input, headcount);
        var severity = Enum.IsDefined(assessment.Severity) && assessment.Severity > rules.Severity
            ? assessment.Severity : rules.Severity;
        var (action, farmerActions) = RuleBasedAnalyzer.GuidanceFor(severity);
        var vision = Mappers.ReadVision(input.VisionAnalysisJson);
        var symptoms = RuleBasedAnalyzer.DetectSymptoms(string.Join(". ", input.Transcript, input.FarmerNotes,
            vision is { Available: true } ? string.Join(". ", vision.DetectedIssues) : null));
        var observations = new List<string>();
        if (input.MortalityCount > 0) observations.Add($"{input.MortalityCount} reported death(s)");
        observations.AddRange(symptoms.Select(s => s.Label.ToLowerInvariant()));
        var context = observations.Count == 0
            ? "The available report does not establish a cause. Unrecognised signs may still need veterinary review."
            : $"Reported signs: {string.Join(", ", observations)}. These signs can have several causes.";
        var source = assessment.Source == RuleBasedAnalyzer.Source ? "Rule-based" : "AI-assisted";
        var language = LanguageCodes.Normalize(input.ReportLanguage);
        var fallback = language is not null and not "en"
            ? "English safety guidance: reviewed translations are not available for this report language. " : string.Empty;
        var summary = $"{fallback}{source} preliminary urgency guidance ({severity}); this is not a diagnosis. {context}";

        // Never persist the model's unreviewed diagnosis, medicine instructions, or action prose.
        return assessment with
        {
            Severity = severity,
            DiagnosisSummary = summary,
            RecommendedAction = action,
            RequiresVetConsultation = severity is Severity.High or Severity.Critical,
            FarmerActions = farmerActions,
            RawAssessment = null
        };
    }

    /// <summary>A farmer may increase urgency, but cannot suppress the assessed safety escalation.</summary>
    public static Severity FinalSeverity(Severity assessed, Severity? requested) =>
        requested is { } value && Enum.IsDefined(value) && value > assessed ? value : assessed;
}
