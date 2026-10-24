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
    private readonly SpitchService _spitch;
    private readonly YarnGptService _yarnGpt;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(
        IMediator mediator,
        SpitchService spitch,
        YarnGptService yarnGpt,
        ILogger<VoiceController> logger)
    {
        _mediator = mediator;
        _spitch = spitch;
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

    /// <summary>Converts care instructions to audio via Spitch TTS (with YarnGPT fallback).</summary>
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

        if (_spitch.IsConfigured)
        {
            try
            {
                var voice = !string.IsNullOrWhiteSpace(request.Voice)
                    ? request.Voice.Trim().ToLowerInvariant()
                    : request.TargetLanguage?.ToLowerInvariant() switch
                    {
                        "yo" => "sade",
                        "ha" => "umar",
                        "ig" => "chiamaka",
                        _ => "amina"
                    };

                var spitchResult = await _spitch.SynthesizeSpeechAsync(
                    request.Text.Trim(),
                    voice,
                    request.TargetLanguage,
                    speed: 1.0m,
                    format: "mp3",
                    ct: ct);

                return Ok(new
                {
                    audioUrl = spitchResult.AudioUrl,
                    provider = "spitch",
                    voice = voice,
                    creditsCharged = 0
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Spitch TTS failed, trying YarnGPT fallback");
            }
        }

        if (_yarnGpt.IsConfigured)
        {
            try
            {
                var ttsResult = await _yarnGpt.SynthesizeSpeechAsync(
                    request.Text.Trim(),
                    request.Voice ?? "umar",
                    outputFormat: "mp3",
                    targetLanguage: request.TargetLanguage,
                    ct: ct);

                return Ok(new
                {
                    audioUrl = ttsResult.AudioUrl,
                    provider = "yarngpt",
                    voice = request.Voice ?? "umar",
                    creditsCharged = ttsResult.CreditsCharged
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "YarnGPT voice response TTS failed");
            }
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            error = "Speech synthesis is unavailable or not configured on this server.",
            code = "tts_unavailable"
        });
    }

    /// <summary>List available voices across configured speech engines.</summary>
    [HttpGet("voices")]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        if (_spitch.IsConfigured)
        {
            return Ok(await _spitch.GetVoicesAsync(ct));
        }

        if (_yarnGpt.IsConfigured)
        {
            try
            {
                return Ok(await _yarnGpt.GetVoicesAsync(ct));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to fetch voices");
            }
        }

        return Ok(Array.Empty<string>());
    }
}

/// <summary>Payload for text-to-speech synthesis.</summary>
public record VoiceRespondRequest(string Text, string? Voice, string? TargetLanguage);
