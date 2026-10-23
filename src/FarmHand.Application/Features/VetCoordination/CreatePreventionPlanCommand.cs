using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Creates or replaces the prevention plan for an incident (farm owner, admin or assigned vet).</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="Content">Plan content.</param>
/// <param name="GeneratedFromHistory">Whether the plan was drafted from farm history.</param>
public record CreatePreventionPlanCommand(Guid IncidentId, string Content, bool GeneratedFromHistory = false) : IRequest<Guid>;

public class CreatePreventionPlanCommandHandler : IRequestHandler<CreatePreventionPlanCommand, Guid>
{
    private readonly IPreventionPlanRepository _preventionPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public CreatePreventionPlanCommandHandler(
        IPreventionPlanRepository preventionPlanRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        IAuditLogger audit)
    {
        _preventionPlanRepository = preventionPlanRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreatePreventionPlanCommand request, CancellationToken ct)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureIncidentWriteAccessAsync(incident.Id, ct);

        var content = request.Content.Trim();
        var existing = await _preventionPlanRepository.GetByIncidentIdAsync(request.IncidentId, ct);
        if (existing is not null)
        {
            existing.UpdateContent(content, request.GeneratedFromHistory);
            await _audit.RecordAsync("prevention_plan.updated", incident.Id, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return existing.Id;
        }

        var plan = new PreventionPlan(request.IncidentId, content, request.GeneratedFromHistory);
        await _preventionPlanRepository.AddAsync(plan, ct);
        await _audit.RecordAsync("prevention_plan.created", incident.Id, ct: ct);
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
