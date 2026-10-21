using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

/// <summary>Creates an incident. DetectedAt is assigned by the server.</summary>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="FlockId">Affected flock id.</param>
/// <param name="IncidentType">Short incident classification.</param>
/// <param name="Severity">Severity level, sent as an integer (0-3).</param>
/// <param name="TriggeringObservations">Evidence that triggered the incident.</param>
/// <param name="EvidenceSummary">Summary shown to the farmer and vet.</param>
/// <param name="RuleVersion">Optional detection rule version.</param>
public record CreateIncidentCommand(
    Guid FarmId,
    Guid FlockId,
    string IncidentType,
    Domain.Enums.Severity Severity,
    string TriggeringObservations,
    string EvidenceSummary,
    string? RuleVersion = null) : IRequest<Guid>;

public class CreateIncidentCommandHandler : IRequestHandler<CreateIncidentCommand, Guid>
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIncidentCommandHandler(
        IIncidentRepository incidentRepository,
        IUnitOfWork unitOfWork)
    {
        _incidentRepository = incidentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateIncidentCommand request, CancellationToken ct)
    {
        var incident = new Incident(
            request.FarmId,
            request.FlockId,
            DateTime.UtcNow,
            request.IncidentType,
            request.Severity,
            request.TriggeringObservations,
            request.EvidenceSummary,
            request.RuleVersion);

        await _incidentRepository.AddAsync(incident, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return incident.Id;
    }
}

public class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.FlockId).NotEqual(Guid.Empty);
        RuleFor(x => x.IncidentType).NotEmpty();
        RuleFor(x => x.EvidenceSummary).NotEmpty();
    }
}
