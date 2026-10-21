using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

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
