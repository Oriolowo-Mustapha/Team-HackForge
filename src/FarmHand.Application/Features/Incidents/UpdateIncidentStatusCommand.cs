using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

/// <summary>Transitions an incident to a new status.</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="NewStatus">Target status, sent as an integer (1-5). Open/unknown values are ignored.</param>
/// <param name="FollowUpDate">Optional follow-up date; defaults to now + 1 day for FollowUpRequired.</param>
public record UpdateIncidentStatusCommand(Guid IncidentId, IncidentStatus NewStatus, DateTime? FollowUpDate = null) : IRequest;

public class UpdateIncidentStatusCommandHandler : IRequestHandler<UpdateIncidentStatusCommand>
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateIncidentStatusCommandHandler(
        IIncidentRepository incidentRepository,
        IUnitOfWork unitOfWork)
    {
        _incidentRepository = incidentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateIncidentStatusCommand request, CancellationToken ct)
    {
        var incident = await _incidentRepository.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

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

        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class UpdateIncidentStatusCommandValidator : AbstractValidator<UpdateIncidentStatusCommand>
{
    public UpdateIncidentStatusCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
    }
}
