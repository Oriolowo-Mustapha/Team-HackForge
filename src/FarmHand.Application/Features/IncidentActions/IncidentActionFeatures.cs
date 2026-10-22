using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.IncidentActions;

/// <summary>Create an action item for an incident.</summary>
/// <param name="IncidentId">Incident id; must be accessible to the caller.</param>
/// <param name="Description">What action to take.</param>
/// <param name="Priority">Action priority.</param>
public record CreateIncidentActionCommand(Guid IncidentId, string Description, string Priority) : IRequest<Guid>;

public class CreateIncidentActionCommandHandler : IRequestHandler<CreateIncidentActionCommand, Guid>
{
    private readonly IIncidentActionRepository _actionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateIncidentActionCommandHandler(
        IIncidentActionRepository actionRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _actionRepository = actionRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateIncidentActionCommand request, CancellationToken ct)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        var action = new IncidentAction(request.IncidentId, request.Description, request.Priority);
        await _actionRepository.AddAsync(action, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return action.Id;
    }
}

public class CreateIncidentActionCommandValidator : AbstractValidator<CreateIncidentActionCommand>
{
    public CreateIncidentActionCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Priority).NotEmpty().MaximumLength(50);
    }
}

/// <summary>List incident actions for an incident.</summary>
/// <param name="IncidentId">Incident id; must be accessible to the caller.</param>
public record GetIncidentActionsQuery(Guid IncidentId) : IRequest<List<IncidentActionDto>>;

public class GetIncidentActionsQueryHandler : IRequestHandler<GetIncidentActionsQuery, List<IncidentActionDto>>
{
    private readonly IIncidentActionRepository _actionRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetIncidentActionsQueryHandler(
        IIncidentActionRepository actionRepository,
        IIncidentRepository incidentRepository,
        IResourceAuthorizationService authorization)
    {
        _actionRepository = actionRepository;
        _incidentRepository = incidentRepository;
        _authorization = authorization;
    }

    public async Task<List<IncidentActionDto>> Handle(GetIncidentActionsQuery request, CancellationToken ct)
    {
        var incident = await _incidentRepository.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        var actions = await _actionRepository.GetByIncidentIdAsync(request.IncidentId, ct);

        return actions.Select(a => new IncidentActionDto(
            a.Id, a.IncidentId, a.Description, a.Priority, a.Status, a.CompletedAt, a.CompletionNotes)).ToList();
    }
}

public class GetIncidentActionsQueryValidator : AbstractValidator<GetIncidentActionsQuery>
{
    public GetIncidentActionsQueryValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
    }
}
