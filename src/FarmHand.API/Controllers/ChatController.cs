using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Poultry;
using FarmHand.Domain.Enums;
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
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IMediator mediator,
        IFarmAiService aiService,
        IVisionAnalysisService visionService,
        ICloudinaryService cloudinary,
        YarnGptService yarnGpt,
        ILogger<ChatController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _visionService = visionService;
        _cloudinary = cloudinary;
        _yarnGpt = yarnGpt;
        _logger = logger;
    }

    /// <summary>
    /// Chat observation: farmer submits text/voice/image observation via AI.
    /// Returns structured observation data WITHOUT creating an incident.
    /// The farmer can review, correct, and then choose to save or create an incident.
    /// </summary>
    [HttpPost("observation")]
    public async Task<IActionResult> ChatObservation(
        [FromForm] ChatObservationRequest request,
        CancellationToken ct = default)
    {
        string? transcript = request.RawText;
        string? imageUrl = null;
        string? visionAnalysis = null;
        string? idempotencyKey = Guid.NewGuid().ToString();

        try
        {
            // 1. Handle voice input via YarnGPT STT
            if (request.AudioFile != null && request.AudioFile.Length > 0)
            {
                using var audioStream = request.AudioFile.OpenReadStream();
                var sttResult = await _yarnGpt.TranscribeAudioAsync(audioStream, request.AudioFile.FileName, idempotencyKey, ct);
                transcript = sttResult.Transcript;
                _logger.LogInformation("Chat STT completed");
            }

            if (string.IsNullOrWhiteSpace(transcript))
                return BadRequest(new { error = "No text or audio provided." });

            // 2. Handle image upload via Cloudinary + Groq Vision
            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                using var imageStream = request.ImageFile.OpenReadStream();
                var uploadResult = await _cloudinary.UploadImageAsync(imageStream, request.ImageFile.FileName, "farmhand/observations", ct);
                imageUrl = uploadResult.SecureUrl;

                var vision = await _visionService.AnalyzeImageAsync(imageUrl, request.RawText, ct);
                visionAnalysis = System.Text.Json.JsonSerializer.Serialize(new
                {
                    vision.Description,
                    vision.DetectedIssues,
                    vision.Confidence,
                    AnalyzedAt = DateTime.UtcNow
                });
            }

            // 3. Extract structured observation via Gemini (no incident created yet)
            var extracted = await _aiService.ExtractObservationAsync(transcript ?? request.RawText, ct);

            // Range validation
            if (extracted.MortalityCount < 0 || extracted.MortalityCount > 10000)
                return BadRequest(new { error = "Invalid mortality count extracted." });

            // 4. Return structured observation for farmer review - NO incident created yet
            var observationPreview = new
            {
                idempotencyKey,
                request.LivestockAnimalId,
                request.LivestockCategoryId,
                transcript,
                imageUrl,
                visionAnalysis = visionAnalysis != null ? System.Text.Json.JsonSerializer.Deserialize<object>(visionAnalysis) : null,
                extracted = new
                {
                    extracted.MortalityCount,
                    extracted.WaterConsumption,
                    extracted.FeedConsumption,
                    extracted.EggProduction,
                    extracted.FarmerNotes
                },
                message = "Observation extracted. Review and confirm to save, or create an incident if concerning."
            };

            return Ok(observationPreview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat observation failed");
            return StatusCode(500, new { error = "Observation processing failed. Please try again." });
        }
    }

    /// <summary>
    /// Confirm and save a chat observation. Creates the PoultryObservation record.
    /// Does NOT create an incident - that's a separate step.
    /// </summary>
    [HttpPost("observation/confirm")]
    public async Task<IActionResult> ConfirmObservation(
        [FromBody] ConfirmChatObservationRequest request,
        CancellationToken ct)
    {
        var command = new RecordObservationCommand(
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            request.RawText,
            request.IdempotencyKey,
            request.ImageUrl,
            new ExtractedObservation(
                request.MortalityCount,
                request.WaterConsumption,
                request.FeedConsumption,
                request.EggProduction,
                request.FarmerNotes),
            PreAnalyzedVisionJson: request.VisionAnalysisJson);

        var observationId = await _mediator.Send(command, ct);

        return CreatedAtAction(nameof(ConfirmObservation), new { }, new
        {
            observationId,
            message = "Observation saved. You can now create an incident if this needs vet attention."
        });
    }

    /// <summary>
    /// Create an incident from a confirmed observation. Only call this if the observation
    /// indicates a concerning issue that needs vet attention.
    /// </summary>
    [HttpPost("observation/{observationId}/create-incident")]
    public async Task<IActionResult> CreateIncidentFromObservation(
        Guid observationId,
        [FromBody] CreateIncidentFromObservationRequest request,
        CancellationToken ct)
    {
        // This would load the observation and create an incident from it
        // For now, delegate to the incidents controller logic via MediatR
        var command = new FarmHand.Application.Features.Incidents.CreateIncidentCommand(
            request.FarmId,
            observationId, // Using observationId as a reference - in production you'd look up the flock/animal
            "Poultry Observation",
            request.Severity,
            request.TriggeringObservations,
            request.EvidenceSummary,
            request.RuleVersion);

        var incidentId = await _mediator.Send(command, ct);

        return CreatedAtAction(nameof(CreateIncidentFromObservation), new { }, new
        {
            incidentId,
            message = "Incident created from observation. Vet attention recommended."
        });
    }
}

public record ConfirmChatObservationRequest(
    Guid LivestockAnimalId,
    Guid? LivestockCategoryId,
    string RawText,
    string IdempotencyKey,
    string? ImageUrl,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    string? VisionAnalysisJson);

public record CreateIncidentFromObservationRequest(
    Guid FarmId,
    Domain.Enums.Severity Severity,
    string TriggeringObservations,
    string EvidenceSummary,
    string? RuleVersion = null);

public record ChatObservationRequest(
    string RawText,
    Guid LivestockAnimalId,
    Guid? LivestockCategoryId,
    IFormFile? AudioFile = null,
    IFormFile? ImageFile = null);
