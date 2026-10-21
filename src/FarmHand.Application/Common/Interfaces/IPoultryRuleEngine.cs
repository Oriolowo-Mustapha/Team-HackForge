namespace FarmHand.Application.Common.Interfaces;

public record RuleEvaluationResult(
    bool IsWarning,
    string IncidentType,
    Domain.Enums.Severity Severity,
    string EvidenceSummary,
    string RuleVersion);

public interface IPoultryRuleEngine
{
    Task<RuleEvaluationResult> EvaluateAsync(
        Guid flockId,
        int mortalityCount,
        string? waterConsumption,
        string? feedConsumption,
        string? eggProduction,
        CancellationToken ct = default);
}
