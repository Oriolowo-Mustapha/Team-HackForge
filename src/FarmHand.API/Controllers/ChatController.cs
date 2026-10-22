using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Incidents;
using FarmHand.Application.Features.Poultry;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFarmAiService _aiService;
    private readonly IVisionAnalysisService _visionService;
    private readonly ICloudinaryService _cloudinary;
    private readonly YarnGptService _yarnGpt;
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IMediator mediator,
        IFarmAiService aiService,
        IVisionAnalysisService visionService,
        ICloudinaryService cloudinary,
        YarnGptService yarnGpt,
        IPoultryObservationRepository observationRepository,
        IResourceAuthorizationService authorization,
        ILogger<ChatController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _visionService = visionService;
        _cloudinary = cloudinary;
        _yarnGpt = yarnGpt;
        _observationRepository = observationRepository;
        _authorization = authorization;
        _logger = logger;
    }

    /// <summary>
    /// Create a pending observation from chat text, voice, and/or image input.
    /// </summary>
    /// <remarks>
    /// The server extracts and stores the observation in Pending state. The response returns
    /// the observation id; call the confirm endpoint after the farmer reviews it. Confirming
    /// does not create an incident.
    /// </remarks>
    /// <response code="202">Pending observation created.</response>
    /// <response code="400">No useful input or animal/category missing.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm or animal belongs to another user.</response>
    /// <response code="404">The farm, animal, or category was not found.</response>
    [HttpPost("observation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChatObservation(
        [FromForm] ChatObservationRequest request,
        CancellationToken ct = default)
    {
        if (request.FarmId == Guid.Empty)
            return BadRequest(new { error = "Farm id is required." });

        if (request.LivestockAnimalId is null && request.LivestockCategoryId is null)
            return BadRequest(new { error = "Provide either a livestock animal or a category." });

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        string? transcript = request.RawText;

        if (request.AudioFile is { Length: > 0 })
        {
            using var audioStream = request.AudioFile.OpenReadStream();
            var sttResult = await _yarnGpt.TranscribeAudioAsync(audioStream, request.AudioFile.FileName, key, ct);
            transcript = sttResult.Transcript;
            _logger.LogInformation("Chat STT completed");
        }

        if (string.IsNullOrWhiteSpace(transcript))
            return BadRequest(new { error = "No text or audio provided." });

        string? imageUrl = null;
        string? visionAnalysisJson = null;

        if (request.ImageFile is { Length: > 0 })
        {
            using var imageStream = request.ImageFile.OpenReadStream();
            var uploadResult = await _cloudinary.UploadImageAsync(imageStream, request.ImageFile.FileName, "farmhand/observations", ct);
            imageUrl = uploadResult.SecureUrl;

            var vision = await _visionService.AnalyzeImageAsync(imageUrl, transcript, ct);
            visionAnalysisJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                vision.Description,
                vision.DetectedIssues,
                vision.Confidence,
                AnalyzedAt = DateTime.UtcNow
            });
        }

        var command = new RecordObservationCommand(
            request.FarmId,
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            transcript,
            key,
            imageUrl,
            PreAnalyzedVisionJson: visionAnalysisJson);

        var observationId = await _mediator.Send(command, ct);

        return Accepted(new
        {
            observationId,
            status = nameof(ObservationConfirmationStatus.Pending),
            request.FarmId,
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            transcript,
            imageUrl,
            visionAnalysis = visionAnalysisJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<object>(visionAnalysisJson),
            message = "Observation saved as pending. Review and confirm it, then optionally create an incident."
        });
    }

    /// <summary>Confirm a pending chat observation.</summary>
    /// <remarks>
    /// When the farmer confirms, the AI assesses severity using farm history. If severity is
    /// High or Critical, an incident is auto-created and linked to the observation.
    /// </remarks>
    /// <response code="200">Observation confirmed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The observation belongs to another user.</response>
    /// <response code="404">Observation not found.</response>
    [HttpPost("observation/{observationId}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmObservation(
        Guid observationId,
        [FromBody] ConfirmChatObservationRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ConfirmObservationCommand(
            observationId,
            request.CorrectedNotes,
            request.OverrideSeverity,
            request.OverrideReason), ct);
        return Ok(new
        {
            observationId = result.ObservationId,
            status = result.ConfirmationStatus,
            aiSeverity = result.AiSeverity,
            finalSeverity = result.FinalSeverity,
            severityOverridden = result.SeverityOverridden,
            aiDiagnosisSummary = result.AiDiagnosisSummary,
            aiRecommendedAction = result.AiRecommendedAction,
            aiRequiresVetConsultation = result.AiRequiresVetConsultation,
            incidentAutoCreated = result.IncidentAutoCreated,
            autoCreatedIncidentId = result.AutoCreatedIncidentId,
            autoCreatedIncidents = result.AutoCreatedIncidents
        });
    }

    /// <summary>
    /// Create an incident from a confirmed observation.
    /// </summary>
    /// <remarks>
    /// Farm, animal, and category are derived from the stored observation, not from the request.
    /// The observation must be confirmed first.
    /// </remarks>
    /// <response code="201">Incident created.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The observation belongs to another user.</response>
    /// <response code="404">Observation not found.</response>
    /// <response code="409">Observation has not been confirmed.</response>
    [HttpPost("observation/{observationId}/create-incident")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateIncidentFromObservation(
        Guid observationId,
        [FromBody] CreateIncidentFromObservationRequest request,
        CancellationToken ct)
    {
        var observation = await _observationRepository.GetByIdAsync(observationId, ct)
            ?? throw new KeyNotFoundException($"Observation {observationId} not found.");

        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);

        if (observation.ConfirmationStatus == ObservationConfirmationStatus.Pending)
            throw new InvalidOperationException("Confirm the observation before creating an incident.");

        var command = new CreateIncidentCommand(
            observation.FarmId,
            observation.LivestockAnimalId,
            observation.LivestockCategoryId,
            string.IsNullOrWhiteSpace(request.IncidentType) ? "Poultry Observation" : request.IncidentType,
            request.Severity,
            string.IsNullOrWhiteSpace(request.TriggeringObservations)
                ? $"Observation {observation.Id}"
                : request.TriggeringObservations,
            string.IsNullOrWhiteSpace(request.EvidenceSummary)
                ? $"Mortality: {observation.MortalityCount}; notes: {observation.FarmerNotes}"
                : request.EvidenceSummary,
            request.RuleVersion);

        var incidentId = await _mediator.Send(command, ct);

        return CreatedAtAction(nameof(CreateIncidentFromObservation), new { observationId }, new
        {
            incidentId,
            message = "Incident created from observation. Vet attention recommended."
        });
    }
}

public record ConfirmChatObservationRequest(
    string? CorrectedNotes,
    FarmHand.Domain.Enums.Severity? OverrideSeverity = null,
    string? OverrideReason = null);

public record CreateIncidentFromObservationRequest(
    Severity Severity,
    string? IncidentType = null,
    string? TriggeringObservations = null,
    string? EvidenceSummary = null,
    string? RuleVersion = null);

public record ChatObservationRequest(
    Guid FarmId,
    string RawText,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string? IdempotencyKey = null,
    IFormFile? AudioFile = null,
    IFormFile? ImageFile = null);
