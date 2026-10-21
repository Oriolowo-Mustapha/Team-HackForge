namespace FarmHand.Application.Common.DTOs;

public record DashboardDto(
    FarmDto Farm,
    List<IncidentDto> Incidents,
    ActionPlanDto ActionPlan,
    List<VetRequestDto> VetRequests);
