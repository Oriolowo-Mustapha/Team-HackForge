using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>
/// Confirms a pending observation after the farmer has reviewed (and optionally corrected) it, then assesses
/// severity using farm history. High or Critical severity automatically opens a health case (incident).
/// Assessment never fails because of an AI outage: the rule-based triage is used and labelled instead.
/// </summary>
/// <param name="ObservationId">Observation id; must match the route value.</param>
/// <param name="CorrectedNotes">Optional corrected notes that replace the extracted farmer notes.</param>
/// <param name="OverrideSeverity">Optional farmer-supplied severity that replaces the assessed severity.</param>
/// <param name="OverrideReason">Optional reason for overriding the assessed severity.</param>
/// <param name="MortalityCount">Optional corrected death count.</param>
/// <param name="WaterConsumption">Optional corrected water note.</param>
/// <param name="FeedConsumption">Optional corrected feed note.</param>
/// <param name="EggProduction">Optional corrected egg note.</param>
public record ConfirmObservationCommand(
    Guid ObservationId,
    string? CorrectedNotes,
    Severity? OverrideSeverity = null,
    string? OverrideReason = null,
    int? MortalityCount = null,
    string? WaterConsumption = null,
    string? FeedConsumption = null,
    string? EggProduction = null) : IRequest<ConfirmObservationResult>;

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
    List<IncidentDto> AutoCreatedIncidents,
    string? AssessmentSource = null,
    bool AssessmentIsAi = false);

public class ConfirmObservationCommandHandler : IRequestHandler<ConfirmObservationCommand, ConfirmObservationResult>
{
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IAIDiagnosisRepository _aiDiagnosisRepository;
    private readonly IFarmAiService _aiService;
    private readonly IFarmHistoryService _farmHistoryService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public ConfirmObservationCommandHandler(
        IPoultryObservationRepository observationRepository,
        IIncidentRepository incidentRepository,
        IAIDiagnosisRepository aiDiagnosisRepository,
        IFarmAiService aiService,
        IFarmHistoryService farmHistoryService,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        IAuditLogger audit)
    {
        _observationRepository = observationRepository;
        _incidentRepository = incidentRepository;
        _aiDiagnosisRepository = aiDiagnosisRepository;
        _aiService = aiService;
        _farmHistoryService = farmHistoryService;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<ConfirmObservationResult> Handle(ConfirmObservationCommand request, CancellationToken ct)
    {
        var observation = await _observationRepository.GetByIdAsync(request.ObservationId, ct)
            ?? throw new KeyNotFoundException($"Observation {request.ObservationId} not found.");

        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);

        // Idempotency: a repeated confirm must not re-run the AI or duplicate incidents.
        var existingDiagnosis = await _aiDiagnosisRepository.GetByObservationIdAsync(observation.Id, ct);
        if (!observation.IsPending && existingDiagnosis is not null)
            return await BuildExistingResultAsync(observation, existingDiagnosis, _incidentRepository, ct);

        if (!observation.IsPending)
            throw PublicException.Conflict("This report has already been confirmed.");

        var notes = request.CorrectedNotes;
        var corrected = observation.ApplyCorrections(
            request.MortalityCount,
            request.WaterConsumption,
            request.FeedConsumption,
            request.EggProduction,
            notes);

        if (corrected) observation.Correct();
        else observation.Confirm();

        var history = await _farmHistoryService.GetFarmHistoryAsync(observation.FarmId, ct);
        var subject = await DescribeSubjectAsync(observation, ct);

        var assessment = await _aiService.AssessSeverityAsync(new AssessmentInput(
            observation.RawTranscript,
            observation.MortalityCount,
            observation.WaterConsumption,
            observation.FeedConsumption,
            observation.EggProduction,
            observation.FarmerNotes,
            observation.VisionAnalysis,
            subject.Description,
            history), ct);

        // Rule-based triage can use the recorded flock size; AI results are kept as returned.
        if (assessment.Source == RuleBasedAnalyzer.Source && subject.Headcount is > 0)
        {
            assessment = RuleBasedAnalyzer.Assess(new AssessmentInput(
                observation.RawTranscript, observation.MortalityCount, observation.WaterConsumption,
                observation.FeedConsumption, observation.EggProduction, observation.FarmerNotes,
                observation.VisionAnalysis, subject.Description, history), subject.Headcount);
        }

        var aiDiagnosis = new AIDiagnosis(
            observation.Id,
            assessment.Severity,
            assessment.DiagnosisSummary,
            assessment.RecommendedAction,
            assessment.RequiresVetConsultation,
            assessment.RawAssessment,
            assessment.Source);

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
                return await BuildExistingResultAsync(observation, concurrentDiagnosis, _incidentRepository, ct);
            throw;
        }

        var autoCreated = new List<IncidentDto>();
        Guid? autoCreatedIncidentId = null;

        // Farmer override wins over the assessed severity when supplied.
        var severityOverridden = request.OverrideSeverity.HasValue && request.OverrideSeverity != assessment.Severity;
        var finalSeverity = request.OverrideSeverity ?? assessment.Severity;
        var requiresVetConsultation = finalSeverity is Severity.High or Severity.Critical;

        // Business rule: only High or Critical severity opens a case automatically.
        if (requiresVetConsultation)
        {
            var evidence = $"{subject.Name}: {assessment.DiagnosisSummary}";
            if (severityOverridden)
            {
                var reason = string.IsNullOrWhiteSpace(request.OverrideReason) ? "no reason given" : request.OverrideReason.Trim();
                evidence += $" [Farmer override: assessed {assessment.Severity} -> {finalSeverity}; reason: {reason}]";
            }

            var incident = new Incident(
                observation.FarmId,
                observation.LivestockAnimalId,
                observation.LivestockCategoryId,
                DateTime.UtcNow,
                Mappers.IsAiSource(assessment.Source) ? "AI-assisted health alert" : "Health alert",
                finalSeverity,
                $"Observation {observation.Id}",
                Truncate(evidence, 1000),
                assessment.Source,
                observation.Id);

            await _incidentRepository.AddAsync(incident, ct);
            aiDiagnosis.LinkIncident(incident.Id);
            await _audit.RecordAsync("incident.auto_created", incident.Id, $"severity={finalSeverity}; observation={observation.Id}", ct: ct);

            autoCreatedIncidentId = incident.Id;
            autoCreated.Add(incident.ToDto());
        }

        await _audit.RecordAsync("observation.confirmed", observation.Id,
            $"severity={finalSeverity}; source={assessment.Source}; corrected={corrected}", ct: ct);
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
            autoCreatedIncidentId is not null,
            autoCreatedIncidentId,
            autoCreated,
            assessment.Source,
            Mappers.IsAiSource(assessment.Source));
    }

    private sealed record SubjectInfo(string Name, string Description, int? Headcount);

    private async Task<SubjectInfo> DescribeSubjectAsync(PoultryObservation observation, CancellationToken ct)
    {
        if (observation.LivestockAnimalId is { } animalId && await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct) is { } animal)
            return new SubjectInfo(animal.Name, $"one {animal.Species.ToLowerInvariant()} named {animal.Name}", null);

        if (observation.LivestockCategoryId is { } categoryId && await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct) is { } category)
            return new SubjectInfo(category.Name,
                $"the group \"{category.Name}\"" + (category.AnimalCount > 0 ? $" of {category.AnimalCount} {category.Species ?? "animals"}" : string.Empty),
                category.AnimalCount > 0 ? category.AnimalCount : null);

        return new SubjectInfo("Unspecified", "an unspecified animal", null);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

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
            : new List<IncidentDto> { existingIncident.ToDto() };

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
            existingIncidents,
            diagnosis.Source,
            Mappers.IsAiSource(diagnosis.Source));
    }
}

public class ConfirmObservationCommandValidator : AbstractValidator<ConfirmObservationCommand>
{
    public ConfirmObservationCommandValidator()
    {
        RuleFor(x => x.ObservationId).NotEqual(Guid.Empty);
        RuleFor(x => x.OverrideReason).MaximumLength(1000);
        RuleFor(x => x.CorrectedNotes).MaximumLength(1000);
        RuleFor(x => x.OverrideSeverity).IsInEnum().When(x => x.OverrideSeverity is not null);
        RuleFor(x => x.MortalityCount).InclusiveBetween(0, 10000).When(x => x.MortalityCount is not null);
        RuleFor(x => x.WaterConsumption).MaximumLength(100);
        RuleFor(x => x.FeedConsumption).MaximumLength(100);
        RuleFor(x => x.EggProduction).MaximumLength(100);
    }
}
