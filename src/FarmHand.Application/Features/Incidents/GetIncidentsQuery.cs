using FarmHand.Application.Common.DTOs;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

public record GetIncidentsQuery(Guid FarmId) : IRequest<List<IncidentDto>>;

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, List<IncidentDto>>
{
    private readonly IIncidentRepository _incidentRepository;

    public GetIncidentsQueryHandler(IIncidentRepository incidentRepository)
    {
        _incidentRepository = incidentRepository;
    }

    public async Task<List<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken ct)
    {
        var incidents = await _incidentRepository.GetByFarmIdAsync(request.FarmId, ct);

        return incidents.Select(i => new IncidentDto(
            i.Id, i.FarmId, i.FlockId, i.DetectedAt, i.IncidentType, i.Severity,
            i.TriggeringObservations, i.EvidenceSummary, i.Status, i.RuleVersion, i.FollowUpDate, i.ResolvedAt
        )).ToList();
    }
}
