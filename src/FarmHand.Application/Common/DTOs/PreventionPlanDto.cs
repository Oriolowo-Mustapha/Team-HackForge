namespace FarmHand.Application.Common.DTOs;

/// <summary>Prevention plan summary.</summary>
/// <param name="Id">Prevention plan id.</param>
/// <param name="IncidentId">Related incident id.</param>
/// <param name="Content">Plan content.</param>
/// <param name="GeneratedFromHistory">Whether the plan was generated from historical data.</param>
public record PreventionPlanDto(
    Guid Id,
    Guid IncidentId,
    string Content,
    bool GeneratedFromHistory);
