using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

public record GetIncidentsQuery(Guid FarmId) : IRequest<List<IncidentDto>>;

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, List<IncidentDto>>
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetIncidentsQueryHandler(
        IIncidentRepository incidentRepository,
        IResourceAuthorizationService authorization)
    {
        _incidentRepository = incidentRepository;
        _authorization = authorization;
    }

    public async Task<List<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var incidents = await _incidentRepository.GetByFarmIdAsync(request.FarmId, ct);

        return incidents.Select(i => new IncidentDto(
            i.Id, i.FarmId, i.LivestockAnimalId, i.LivestockCategoryId, i.DetectedAt, i.IncidentType, i.Severity,
            i.TriggeringObservations, i.EvidenceSummary, i.Status, i.RuleVersion, i.FollowUpDate, i.ResolvedAt
        )).ToList();
    }
}
