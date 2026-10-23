using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Confirms an AI-extracted observation, assesses severity with farm history,
/// and auto-creates an incident when severity is High or Critical.</summary>
/// <param name="ObservationId">Observation id; must match the route value.</param>
/// <param name="CorrectedNotes">Optional corrected notes that replace the extracted farmer notes.</param>
/// <param name="OverrideSeverity">Optional farmer-supplied severity that replaces the AI severity.</param>
/// <param name="OverrideReason">Optional reason for overriding the AI severity.</param>
public record ConfirmObservationCommand(
    Guid ObservationId,
    string? CorrectedNotes,
    FarmHand.Domain.Enums.Severity? OverrideSeverity = null,
    string? OverrideReason = null) : IRequest<ConfirmObservationResult>;

public record ConfirmObservationResult(
    Guid ObservationId,
    string ConfirmationStatus,
    string? AiSeverity,
    string? FinalSeverity,
    bool SeverityOverridden,
    string? AiDiagnosisSummary,
    string? AiRecommendedAction,
    bool AiRequiresVetConsultation,
    bool IncidentAutoCreated,
    Guid? AutoCreatedIncidentId,
    List<IncidentDto> AutoCreatedIncidents);

public class ConfirmObservationCommandHandler : IRequestHandler<ConfirmObservationCommand, ConfirmObservationResult>
{
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IAIDiagnosisRepository _aiDiagnosisRepository;
    private readonly IFarmAiService _aiService;
    private readonly IFarmHistoryService _farmHistoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public ConfirmObservationCommandHandler(
        IPoultryObservationRepository observationRepository,
        IIncidentRepository incidentRepository,
        IAIDiagnosisRepository aiDiagnosisRepository,
        IFarmAiService aiService,
        IFarmHistoryService farmHistoryService,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _observationRepository = observationRepository;
        _incidentRepository = incidentRepository;
        _aiDiagnosisRepository = aiDiagnosisRepository;
        _aiService = aiService;
        _farmHistoryService = farmHistoryService;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<ConfirmObservationResult> Handle(ConfirmObservationCommand request, CancellationToken ct)
    {
        var observation = await _observationRepository.GetByIdAsync(request.ObservationId, ct)
            ?? throw new KeyNotFoundException($"Observation {request.ObservationId} not found.");

        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);

        // Idempotency: a repeated confirm must not re-run the AI or duplicate incidents.
        var existingDiagnosis = await _aiDiagnosisRepository.GetByObservationIdAsync(observation.Id, ct);
        if (observation.ConfirmationStatus == FarmHand.Domain.Enums.ObservationConfirmationStatus.Confirmed
            && existingDiagnosis is not null)
        {
            var existingIncident = existingDiagnosis.IncidentId is { } incidentId
                ? await _incidentRepository.GetByIdAsync(incidentId, ct)
                : null;

            var existingIncidents = existingIncident is null
                ? new List<IncidentDto>()
                : new List<IncidentDto>
                {
                    new(existingIncident.Id, existingIncident.FarmId, existingIncident.LivestockAnimalId,
                        existingIncident.LivestockCategoryId, existingIncident.DetectedAt,
                        existingIncident.IncidentType, existingIncident.Severity,
                        existingIncident.TriggeringObservations, existingIncident.EvidenceSummary,
                        existingIncident.Status, existingIncident.RuleVersion,
                        existingIncident.FollowUpDate, existingIncident.ResolvedAt)
                };

            return new ConfirmObservationResult(
                observation.Id,
                observation.ConfirmationStatus.ToString(),
                existingDiagnosis.Severity.ToString(),
                existingIncident?.Severity.ToString() ?? existingDiagnosis.Severity.ToString(),
                existingIncident is not null && existingIncident.Severity != existingDiagnosis.Severity,
                existingDiagnosis.DiagnosisSummary,
                existingDiagnosis.RecommendedAction,
                existingDiagnosis.RequiresVetConsultation,
                existingDiagnosis.IncidentCreated,
                existingDiagnosis.IncidentId,
                existingIncidents);
        }

        observation.Confirm(request.CorrectedNotes);

        // Re-check for a concurrent confirmation create just before doing expensive AI work.
        var freshExistingDiagnosis = await _aiDiagnosisRepository.GetByObservationIdAsync(observation.Id, ct);
        if (freshExistingDiagnosis is not null)
        {
            return await BuildExistingResultAsync(
                observation,
                freshExistingDiagnosis,
                _incidentRepository,
                ct);
        }

        // Get farm history context for AI assessment
        var history = await _farmHistoryService.GetFarmHistoryAsync(observation.FarmId, ct);

        // AI severity assessment using farm history
        var assessment = await _aiService.AssessSeverityAsync(
            observation.FarmerNotes ?? string.Empty,
            observation.FarmerNotes,
            observation.VisionAnalysis,
            history,
            ct);

        // Persist the AI diagnosis
        var aiDiagnosis = new AIDiagnosis(
            observation.Id,
            assessment.Severity,
            assessment.DiagnosisSummary,
            assessment.RecommendedAction,
            assessment.RequiresVetConsultation,
            assessment.RawAssessment);

        await _aiDiagnosisRepository.AddAsync(aiDiagnosis, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (
            ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
        {
            var concurrentDiagnosis = await _aiDiagnosisRepository.GetByObservationIdAsync(observation.Id, ct);
            if (concurrentDiagnosis is not null)
            {
                return await BuildExistingResultAsync(observation, concurrentDiagnosis, _incidentRepository, ct);
            }

            throw;
        }

        var autoCreated = new List<IncidentDto>();
        Guid? autoCreatedIncidentId = null;
        bool incidentAutoCreated = false;

        // Farmer override wins over the AI severity when supplied
        var severityOverridden = request.OverrideSeverity.HasValue;
        var finalSeverity = request.OverrideSeverity ?? assessment.Severity;
        var requiresVetConsultation = finalSeverity is FarmHand.Domain.Enums.Severity.High or FarmHand.Domain.Enums.Severity.Critical;

        // Auto-create incident only when severity is High or Critical
        if (finalSeverity is FarmHand.Domain.Enums.Severity.High or FarmHand.Domain.Enums.Severity.Critical)
        {
            var evidence = $"Observation {observation.Id}: {assessment.DiagnosisSummary}";
            if (severityOverridden)
            {
                var reason = string.IsNullOrWhiteSpace(request.OverrideReason)
                    ? "no reason given"
                    : request.OverrideReason.Trim();
                evidence += $" [Farmer override: AI={assessment.Severity} -> {finalSeverity}; reason: {reason}]";
            }

            var incident = new Incident(
                observation.FarmId,
                observation.LivestockAnimalId,
                observation.LivestockCategoryId,
                DateTime.UtcNow,
                "AI Auto-Detected",
                finalSeverity,
                evidence,
                observation.FarmerNotes ?? string.Empty,
                "ai-assessment");

            await _incidentRepository.AddAsync(incident, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Link the incident to the AI diagnosis
            aiDiagnosis.LinkIncident(incident.Id);

            autoCreatedIncidentId = incident.Id;
            incidentAutoCreated = true;

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
            assessment.Severity.ToString(),
            finalSeverity.ToString(),
            severityOverridden,
            assessment.DiagnosisSummary,
            assessment.RecommendedAction,
            requiresVetConsultation,
            incidentAutoCreated,
            autoCreatedIncidentId,
            autoCreated);
    }

    private static async Task<ConfirmObservationResult> BuildExistingResultAsync(
        PoultryObservation observation,
        AIDiagnosis diagnosis,
        IIncidentRepository incidentRepository,
        CancellationToken ct)
    {
        var existingIncident = diagnosis.IncidentId is { } incidentId
            ? await incidentRepository.GetByIdAsync(incidentId, ct)
            : null;
        var existingIncidents = existingIncident is null
            ? new List<IncidentDto>()
            : new List<IncidentDto>
            {
                new(existingIncident.Id, existingIncident.FarmId, existingIncident.LivestockAnimalId,
                    existingIncident.LivestockCategoryId, existingIncident.DetectedAt,
                    existingIncident.IncidentType, existingIncident.Severity,
                    existingIncident.TriggeringObservations, existingIncident.EvidenceSummary,
                    existingIncident.Status, existingIncident.RuleVersion,
                    existingIncident.FollowUpDate, existingIncident.ResolvedAt)
            };

        return new ConfirmObservationResult(
            observation.Id,
            observation.ConfirmationStatus.ToString(),
            diagnosis.Severity.ToString(),
            existingIncident?.Severity.ToString() ?? diagnosis.Severity.ToString(),
            existingIncident is not null && existingIncident.Severity != diagnosis.Severity,
            diagnosis.DiagnosisSummary,
            diagnosis.RecommendedAction,
            diagnosis.RequiresVetConsultation,
            diagnosis.IncidentCreated,
            diagnosis.IncidentId,
            existingIncidents);
    }
}

public class ConfirmObservationCommandValidator : AbstractValidator<ConfirmObservationCommand>
{
    public ConfirmObservationCommandValidator()
    {
        RuleFor(x => x.ObservationId).NotEqual(Guid.Empty);
        RuleFor(x => x.OverrideReason).MaximumLength(1000);
    }
}
