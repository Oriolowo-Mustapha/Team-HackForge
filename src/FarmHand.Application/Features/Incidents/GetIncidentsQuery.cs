using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

/// <summary>List incidents for a farm with pagination.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record GetIncidentsQuery(Guid FarmId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<IncidentDto>>;

public class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, PagedResult<IncidentDto>>
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

    public async Task<PagedResult<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var incidents = (await _incidentRepository.GetByFarmIdAsync(request.FarmId, ct))
            .OrderByDescending(i => i.DetectedAt)
            .ToList();

        var totalCount = incidents.Count;
        var page = Math.Clamp(request.Page, 1, int.MaxValue);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pageItems = incidents
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new IncidentDto(
                i.Id, i.FarmId, i.LivestockAnimalId, i.LivestockCategoryId, i.DetectedAt, i.IncidentType, i.Severity,
                i.TriggeringObservations, i.EvidenceSummary, i.Status, i.RuleVersion, i.FollowUpDate, i.ResolvedAt))
            .ToList();

        return new PagedResult<IncidentDto>(pageItems, page, pageSize, totalCount, totalPages);
    }
}

public class GetIncidentsQueryValidator : AbstractValidator<GetIncidentsQuery>
{
    public GetIncidentsQueryValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).LessThanOrEqualTo(100);
    }
}
