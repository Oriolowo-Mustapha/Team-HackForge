namespace FarmHand.Application.Common.Interfaces;

/// <summary>Result of evaluating a single rule against an observation.</summary>
/// <param name="ShouldCreateIncident">Whether the rule recommends creating an incident.</param>
/// <param name="IncidentType">Suggested incident type when the rule fires.</param>
/// <param name="Severity">Suggested severity when the rule fires.</param>
/// <param name="Reason">Human-readable reason the rule fired.</param>
public record RuleResult(
    bool ShouldCreateIncident,
    string? IncidentType,
    string? Severity,
    string Reason);

/// <summary>Evaluates poultry observations against business rules to detect incidents automatically.</summary>
public interface IPoultryRuleEngine
{
    /// <summary>Evaluate an observation and return all matching rule results.</summary>
    /// <param name="mortalityCount">Observed mortality count.</param>
    /// <param name="farmerNotes">Farmer notes from the observation.</param>
    /// <param name="visionAnalysisJson">Optional vision analysis JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rule results; any with <see cref="RuleResult.ShouldCreateIncident"/> true should trigger incident creation.</returns>
    Task<IReadOnlyList<RuleResult>> EvaluateAsync(int mortalityCount, string? farmerNotes, string? visionAnalysisJson, CancellationToken ct = default);
}
