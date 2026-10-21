namespace FarmHand.Application.Common.DTOs;

/// <summary>Aggregated farm dashboard payload.</summary>
/// <param name="Farm">Farm summary.</param>
/// <param name="Incidents">Incidents recorded for the farm, newest first.</param>
/// <param name="ActionPlan">Derived action items.</param>
/// <param name="VetRequests">Vet requests for the farm. Currently always empty.</param>
public record DashboardDto(
    FarmDto Farm,
    List<IncidentDto> Incidents,
    ActionPlanDto ActionPlan,
    List<VetRequestDto> VetRequests);
