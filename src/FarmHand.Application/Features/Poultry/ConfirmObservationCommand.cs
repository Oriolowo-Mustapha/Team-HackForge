using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Confirms an AI-extracted observation and auto-creates incidents when rules fire.</summary>
/// <param name="ObservationId">Observation id; must match the route value.</param>
/// <param name="CorrectedNotes">Optional corrected notes that replace the extracted farmer notes.</param>
public record ConfirmObservationCommand(Guid ObservationId, string? CorrectedNotes) : IRequest<ConfirmObservationResult>;

public record ConfirmObservationResult(
    Guid ObservationId,
    string ConfirmationStatus,
    List<IncidentDto> AutoCreatedIncidents);

public class ConfirmObservationCommandHandler : IRequestHandler<ConfirmObservationCommand, ConfirmObservationResult>
{
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IPoultryRuleEngine _ruleEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public ConfirmObservationCommandHandler(
        IPoultryObservationRepository observationRepository,
        IIncidentRepository incidentRepository,
        IPoultryRuleEngine ruleEngine,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _observationRepository = observationRepository;
        _incidentRepository = incidentRepository;
        _ruleEngine = ruleEngine;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<ConfirmObservationResult> Handle(ConfirmObservationCommand request, CancellationToken ct)
    {
        var observation = await _observationRepository.GetByIdAsync(request.ObservationId, ct)
            ?? throw new KeyNotFoundException($"Observation {request.ObservationId} not found.");

        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);

        observation.Confirm(request.CorrectedNotes);

        var autoCreated = new List<IncidentDto>();

        // Run the rule engine to auto-detect incidents from the confirmed observation.
        var ruleResults = await _ruleEngine.EvaluateAsync(
            observation.MortalityCount,
            observation.FarmerNotes,
            observation.VisionAnalysis,
            ct);

        foreach (var result in ruleResults.Where(r => r.ShouldCreateIncident))
        {
            var incident = new Incident(
                observation.FarmId,
                observation.LivestockAnimalId,
                observation.LivestockCategoryId,
                DateTime.UtcNow,
                result.IncidentType ?? "Poultry Observation",
                Enum.TryParse<FarmHand.Domain.Enums.Severity>(result.Severity, out var severity) ? severity : FarmHand.Domain.Enums.Severity.Medium,
                $"Observation {observation.Id}: {result.Reason}",
                observation.FarmerNotes ?? string.Empty,
                "auto-rule-engine");

            await _incidentRepository.AddAsync(incident, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            autoCreated.Add(new IncidentDto(
                incident.Id, incident.FarmId, incident.LivestockAnimalId, incident.LivestockCategoryId,
                incident.DetectedAt, incident.IncidentType, incident.Severity,
                incident.TriggeringObservations, incident.EvidenceSummary, incident.Status,
                incident.RuleVersion, incident.FollowUpDate, incident.ResolvedAt));
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new ConfirmObservationResult(
            observation.Id,
            observation.ConfirmationStatus.ToString(),
            autoCreated);
    }
}

public class ConfirmObservationCommandValidator : AbstractValidator<ConfirmObservationCommand>
{
    public ConfirmObservationCommandValidator()
    {
        RuleFor(x => x.ObservationId).NotEqual(Guid.Empty);
    }
}
