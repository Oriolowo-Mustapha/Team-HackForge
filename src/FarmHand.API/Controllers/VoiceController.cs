using FarmHand.Application.Features.Observations;
using FarmHand.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmHand.API.Controllers;

/// <summary>Voice and image check-ins (legacy routes; new clients use <c>POST /api/observations</c>) and text-to-speech.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class VoiceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly YarnGptService _yarnGpt;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(IMediator mediator, YarnGptService yarnGpt, ILogger<VoiceController> logger)
    {
        _mediator = mediator;
        _yarnGpt = yarnGpt;
        _logger = logger;
    }

    /// <summary>Voice check-in: transcribes the audio and stores a pending observation.</summary>
    /// <remarks>
    /// Send multipart/form-data with the audio in <c>audioFile</c>; pass <c>farmId</c>, at least one of
    /// <c>livestockAnimalId</c>/<c>livestockCategoryId</c>, and <c>idempotencyKey</c> as query-string parameters.
    /// </remarks>
    [HttpPost("check-in")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ai")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> VoiceCheckIn(
        IFormFile? audioFile,
        [FromQuery] Guid farmId,
        [FromQuery] string idempotencyKey,
        [FromQuery] Guid? livestockAnimalId = null,
        [FromQuery] Guid? livestockCategoryId = null,
        CancellationToken ct = default)
    {
        if (audioFile == null || audioFile.Length == 0)
            return BadRequest(new { error = "No audio file provided." });

        var result = await _mediator.Send(new SubmitObservationCommand(
            farmId, livestockAnimalId, livestockCategoryId, null,
            await ObservationsController.ReadAsync(audioFile, ct), audioFile.FileName, null,
            idempotencyKey ?? string.Empty), ct);

        return Accepted(new
        {
            observationId = result.Id,
            status = result.ConfirmationStatus.ToString(),
            reused = result.Reused,
            transcript = result.RawTranscript,
            extracted = new { result.MortalityCount, result.WaterConsumption, result.FeedConsumption, result.EggProduction, result.FarmerNotes },
            analysisSource = result.AnalysisSource
        });
    }

    /// <summary>Image check-in: analyses the photo and stores a pending observation.</summary>
    [HttpPost("image-check-in")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ai")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImageCheckIn(
        IFormFile imageFile,
        [FromQuery] Guid farmId,
        [FromQuery] string idempotencyKey,
        [FromQuery] Guid? livestockAnimalId = null,
        [FromQuery] Guid? livestockCategoryId = null,
        [FromQuery] string? farmerNotes = null,
        CancellationToken ct = default)
    {
        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { error = "No image file provided." });

        var result = await _mediator.Send(new SubmitObservationCommand(
            farmId, livestockAnimalId, livestockCategoryId, farmerNotes,
            null, null, await ObservationsController.ReadAsync(imageFile, ct),
            idempotencyKey ?? string.Empty), ct);

        return Accepted(new
        {
            observationId = result.Id,
            status = result.ConfirmationStatus.ToString(),
            reused = result.Reused,
            imageUrl = result.ImageUrl,
            visionAnalysis = result.Vision,
            extracted = new { result.MortalityCount, result.WaterConsumption, result.FeedConsumption, result.EggProduction, result.FarmerNotes },
            analysisSource = result.AnalysisSource
        });
    }

    /// <summary>Converts care instructions to audio via YarnGPT TTS (Pidgin/Yoruba/Hausa/Igbo support).</summary>
    /// <response code="200">Synthesized audio URL.</response>
    /// <response code="503">Speech synthesis is not configured or unavailable.</response>
    [HttpPost("respond")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> VoiceRespond([FromBody] VoiceRespondRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required." });
        if (request.Text.Length > 1500)
            return BadRequest(new { error = "Text must be 1,500 characters or fewer." });
        if (!_yarnGpt.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Speech synthesis is not configured on this server.", code = "tts_unavailable" });

        try
        {
            var ttsResult = await _yarnGpt.SynthesizeSpeechAsync(request.Text.Trim(), request.Voice ?? "umar", outputFormat: "mp3", targetLanguage: request.TargetLanguage, ct);
            return Ok(new { audioUrl = ttsResult.AudioUrl, creditsCharged = ttsResult.CreditsCharged });
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Voice response TTS failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Speech synthesis is unavailable.", code = "tts_unavailable" });
        }
    }

    /// <summary>List available YarnGPT voices.</summary>
    [HttpGet("voices")]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        if (!_yarnGpt.IsConfigured)
            return Ok(Array.Empty<string>());
        try
        {
            return Ok(await _yarnGpt.GetVoicesAsync(ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to fetch voices");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Voice service is unavailable." });
        }
    }
}

/// <summary>Payload for text-to-speech synthesis.</summary>
public record VoiceRespondRequest(string Text, string? Voice, string? TargetLanguage);
