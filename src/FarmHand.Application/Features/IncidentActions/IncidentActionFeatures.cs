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

        await _authorization.EnsureIncidentWriteAccessAsync(incident.Id, ct);

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

        await _authorization.EnsureIncidentAccessAsync(incident.Id, ct);

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

/// <summary>Marks an incident action as completed (owner, admin or assigned vet). Completing twice is a no-op.</summary>
public record CompleteIncidentActionCommand(Guid IncidentId, Guid ActionId, string? Notes) : IRequest<IncidentActionDto>;

public class CompleteIncidentActionCommandHandler : IRequestHandler<CompleteIncidentActionCommand, IncidentActionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public CompleteIncidentActionCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<IncidentActionDto> Handle(CompleteIncidentActionCommand request, CancellationToken ct)
    {
        await _authorization.EnsureIncidentWriteAccessAsync(request.IncidentId, ct);

        var action = await _unitOfWork.IncidentActions.GetByIdAsync(request.ActionId, ct);
        if (action is null || action.IncidentId != request.IncidentId)
            throw new KeyNotFoundException("Action not found for this case.");

        if (!action.IsCompleted)
        {
            action.Complete(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());
            await _audit.RecordAsync("incident.action_completed", action.Id, ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new IncidentActionDto(action.Id, action.IncidentId, action.Description, action.Priority, action.Status, action.CompletedAt, action.CompletionNotes);
    }
}

public class CompleteIncidentActionCommandValidator : AbstractValidator<CompleteIncidentActionCommand>
{
    public CompleteIncidentActionCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.ActionId).NotEqual(Guid.Empty);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
