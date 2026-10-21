using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Farms;

public record GetDashboardQuery(Guid FarmId) : IRequest<DashboardDto?>;

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto?>
{
    private readonly IFarmRepository _farmRepository;
    private readonly IIncidentRepository _incidentRepository;

    public GetDashboardQueryHandler(IFarmRepository farmRepository, IIncidentRepository incidentRepository)
    {
        _farmRepository = farmRepository;
        _incidentRepository = incidentRepository;
    }

    public async Task<DashboardDto?> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var farm = await _farmRepository.GetByIdAsync(request.FarmId, ct);
        if (farm is null) return null;

        var incidents = (await _incidentRepository.GetByFarmIdAsync(request.FarmId, ct))
            .Select(i => new IncidentDto(
                i.Id, i.FarmId, i.FlockId, i.DetectedAt, i.IncidentType, i.Severity,
                i.TriggeringObservations, i.EvidenceSummary, i.Status, i.RuleVersion, i.FollowUpDate, i.ResolvedAt))
            .ToList();

        var actionPlan = new ActionPlanDto(
            incidents.Where(i => i.Severity == Domain.Enums.Severity.Critical || i.Severity == Domain.Enums.Severity.High)
                .Select(i => $"Review incident {i.Id}: {i.IncidentType}").ToList(),
            incidents.Where(i => i.Status == Domain.Enums.IncidentStatus.FollowUpRequired)
                .Select(i => $"Follow up on incident {i.Id}").ToList(),
            new List<string> { "Record daily poultry observations", "Check goat breeding reminders", "Inspect crop plots" }
        );

        return new DashboardDto(
            new FarmDto(farm.Id, farm.OwnerId, farm.DisplayName, farm.Location, farm.CreatedAt),
            incidents,
            actionPlan,
            new List<VetRequestDto>()
        );
    }
}
