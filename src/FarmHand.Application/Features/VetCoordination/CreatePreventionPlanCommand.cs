using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Create a prevention plan for an incident.</summary>
/// <param name="IncidentId">Incident id; must be accessible to the caller.</param>
/// <param name="Content">Plan content.</param>
/// <param name="GeneratedFromHistory">Whether the plan was generated from historical data.</param>
public record CreatePreventionPlanCommand(Guid IncidentId, string Content, bool GeneratedFromHistory = true) : IRequest<Guid>;

public class CreatePreventionPlanCommandHandler : IRequestHandler<CreatePreventionPlanCommand, Guid>
{
    private readonly IPreventionPlanRepository _preventionPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreatePreventionPlanCommandHandler(
        IPreventionPlanRepository preventionPlanRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _preventionPlanRepository = preventionPlanRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreatePreventionPlanCommand request, CancellationToken ct)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        var plan = new PreventionPlan(request.IncidentId, request.Content, request.GeneratedFromHistory);
        await _preventionPlanRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return plan.Id;
    }
}

public class CreatePreventionPlanCommandValidator : AbstractValidator<CreatePreventionPlanCommand>
{
    public CreatePreventionPlanCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}
