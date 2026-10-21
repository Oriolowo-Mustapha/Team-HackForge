using FarmHand.Application.Common.DTOs;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

public record GetPreventionPlanQuery(Guid IncidentId) : IRequest<string?>;

public class GetPreventionPlanQueryHandler : IRequestHandler<GetPreventionPlanQuery, string?>
{
    private readonly IPreventionPlanRepository _preventionPlanRepository;

    public GetPreventionPlanQueryHandler(IPreventionPlanRepository preventionPlanRepository)
    {
        _preventionPlanRepository = preventionPlanRepository;
    }

    public async Task<string?> Handle(GetPreventionPlanQuery request, CancellationToken ct)
    {
        var plan = await _preventionPlanRepository.GetByIncidentIdAsync(request.IncidentId, ct);
        return plan?.Content;
    }
}
