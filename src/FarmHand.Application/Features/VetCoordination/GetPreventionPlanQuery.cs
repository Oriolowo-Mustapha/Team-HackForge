using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

public record GetPreventionPlanQuery(Guid IncidentId) : IRequest<string?>;

public class GetPreventionPlanQueryHandler : IRequestHandler<GetPreventionPlanQuery, string?>
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

    public async Task<string?> Handle(GetPreventionPlanQuery request, CancellationToken ct)
    {
        await _authorization.EnsureIncidentAccessAsync(request.IncidentId, ct);
        var plan = await _preventionPlanRepository.GetByIncidentIdAsync(request.IncidentId, ct);
        return plan?.Content;
    }
}
