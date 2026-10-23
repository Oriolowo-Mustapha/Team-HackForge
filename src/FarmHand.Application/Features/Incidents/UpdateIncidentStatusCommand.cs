using FarmHand.Domain.Interfaces;
using FarmHand.Domain.Enums;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

/// <summary>Transitions an incident to a new status.</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="NewStatus">Target status, sent as an integer (1-5). Open is not a valid target.</param>
/// <param name="FollowUpDate">Optional follow-up date; defaults to now + 1 day for FollowUpRequired.</param>
public record UpdateIncidentStatusCommand(Guid IncidentId, IncidentStatus NewStatus, DateTime? FollowUpDate = null) : IRequest;

public class UpdateIncidentStatusCommandHandler : IRequestHandler<UpdateIncidentStatusCommand>
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Application.Common.Interfaces.IResourceAuthorizationService _authorization;
    private readonly Application.Common.Interfaces.IAuditLogger _audit;

    public UpdateIncidentStatusCommandHandler(
        IIncidentRepository incidentRepository,
        IUnitOfWork unitOfWork,
        Application.Common.Interfaces.IResourceAuthorizationService authorization,
        Application.Common.Interfaces.IAuditLogger audit)
    {
        _audit = audit;
        _incidentRepository = incidentRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Unit> Handle(UpdateIncidentStatusCommand request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.NewStatus) || request.NewStatus == IncidentStatus.Open)
            throw new ArgumentException("A valid target status (1-5) is required.", nameof(request.NewStatus));

        var incident = await _incidentRepository.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureIncidentWriteAccessAsync(incident.Id, ct);

        if (!incident.AllowedTransitions.Contains(request.NewStatus))
            throw Common.Exceptions.PublicException.Conflict($"A case that is {incident.Status} cannot be moved to {request.NewStatus}.");

        var previous = incident.Status;

        switch (request.NewStatus)
        {
            case IncidentStatus.Acknowledged:
                incident.Acknowledge();
                break;
            case IncidentStatus.UnderMonitoring:
                incident.SetUnderMonitoring(request.FollowUpDate);
                break;
            case IncidentStatus.FollowUpRequired:
                incident.RequireFollowUp(request.FollowUpDate ?? DateTime.UtcNow.AddDays(1));
                break;
            case IncidentStatus.Resolved:
                incident.Resolve();
                break;
            case IncidentStatus.Reopened:
                incident.Reopen();
                break;
        }

        await _audit.RecordAsync("incident.status_changed", incident.Id, $"{previous} -> {incident.Status}", ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class UpdateIncidentStatusCommandValidator : AbstractValidator<UpdateIncidentStatusCommand>
{
    public UpdateIncidentStatusCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.NewStatus)
            .IsInEnum()
            .NotEqual(IncidentStatus.Open);
    }
}
