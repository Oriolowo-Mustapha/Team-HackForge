using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

public record GetPreventionPlanQuery(Guid IncidentId) : IRequest<PreventionPlanDto?>;

public class GetPreventionPlanQueryHandler : IRequestHandler<GetPreventionPlanQuery, PreventionPlanDto?>
{
    private readonly IPreventionPlanRepository _preventionPlanRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetPreventionPlanQueryHandler(
        IPreventionPlanRepository preventionPlanRepository,
        IResourceAuthorizationService authorization)
    {
        _preventionPlanRepository = preventionPlanRepository;
        _authorization = authorization;
    }

    public async Task<PreventionPlanDto?> Handle(GetPreventionPlanQuery request, CancellationToken ct)
    {
        await _authorization.EnsureIncidentAccessAsync(request.IncidentId, ct);
        var plan = await _preventionPlanRepository.GetByIncidentIdAsync(request.IncidentId, ct);
        if (plan is null) return null;

        return new PreventionPlanDto(plan.Id, plan.IncidentId, plan.Content, plan.GeneratedFromHistory);
    }
}
