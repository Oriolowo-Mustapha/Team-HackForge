using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Poultry;
using FarmHand.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Voice and image check-ins powered by YarnGPT, Gemini, and Groq.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class VoiceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IFarmAiService _aiService;
    private readonly IVisionAnalysisService _visionService;
    private readonly YarnGptService _yarnGpt;
    private readonly ICloudinaryService _cloudinary;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(
        IMediator mediator,
        IFarmAiService aiService,
        IVisionAnalysisService visionService,
        YarnGptService yarnGpt,
        ICloudinaryService cloudinary,
        ILogger<VoiceController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _visionService = visionService;
        _yarnGpt = yarnGpt;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    /// <summary>
    /// Voice check-in: uploads audio, transcribes via YarnGPT STT, extracts structured observation via Gemini.
    /// </summary>
    /// <remarks>
    /// Send multipart/form-data with the audio in <c>audioFile</c>; pass <c>farmId</c>,
    /// at least one of <c>livestockAnimalId</c>/<c>livestockCategoryId</c>, and
    /// <c>idempotencyKey</c> as query-string parameters. Reusing an idempotency key returns
    /// the existing observation.
    /// </remarks>
    /// <param name="audioFile">Audio recording (webm/mp3/wav).</param>
    /// <param name="farmId">Farm that owns the observation.</param>
    /// <param name="idempotencyKey">Client-generated key for retry safety.</param>
    /// <param name="livestockAnimalId">Optional animal id for animal-level observations.</param>
    /// <param name="livestockCategoryId">Optional category id for flock-level observations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Observation created from the transcript.</response>
    /// <response code="400">Missing audio file, idempotency key, or animal/category.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm or animal belongs to another user.</response>
    /// <response code="404">The farm, animal, or category was not found.</response>
    [HttpPost("check-in")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency key is required." });

        if (farmId == Guid.Empty)
            return BadRequest(new { error = "Farm id is required." });

        if (livestockAnimalId is null && livestockCategoryId is null)
            return BadRequest(new { error = "Provide either a livestock animal or a category." });

        using var audioStream = audioFile.OpenReadStream();
        var sttResult = await _yarnGpt.TranscribeAudioAsync(audioStream, audioFile.FileName, idempotencyKey, ct);
        _logger.LogInformation("YarnGPT STT completed for voice check-in");

        var extracted = await _aiService.ExtractObservationAsync(sttResult.Transcript, ct);

        var command = new RecordObservationCommand(
            farmId,
            livestockAnimalId,
            livestockCategoryId,
            sttResult.Transcript,
            idempotencyKey,
            PreExtracted: extracted);

        var observationId = await _mediator.Send(command, ct);

        return Accepted(new
        {
            observationId,
            transcript = sttResult.Transcript,
            extracted = new
            {
                extracted.MortalityCount,
                extracted.WaterConsumption,
                extracted.FeedConsumption,
                extracted.EggProduction,
                extracted.FarmerNotes
            },
            creditsCharged = sttResult.CreditsCharged
        });
    }

    /// <summary>
    /// Image check-in: uploads image to Cloudinary, analyzes via Groq Vision, extracts via Gemini.
    /// </summary>
    /// <remarks>
    /// Send multipart/form-data with the image in <c>imageFile</c>; pass <c>farmId</c>,
    /// at least one of <c>livestockAnimalId</c>/<c>livestockCategoryId</c>,
    /// <c>idempotencyKey</c>, and optional <c>farmerNotes</c> as query-string parameters.
    /// </remarks>
    /// <param name="imageFile">Image of the flock or environment.</param>
    /// <param name="farmId">Farm that owns the observation.</param>
    /// <param name="idempotencyKey">Client-generated key for retry safety.</param>
    /// <param name="livestockAnimalId">Optional animal id for animal-level observations.</param>
    /// <param name="livestockCategoryId">Optional category id for flock-level observations.</param>
    /// <param name="farmerNotes">Optional notes to combine with vision output.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Observation created from the image analysis.</response>
    /// <response code="400">Missing image file, idempotency key, or animal/category.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm or animal belongs to another user.</response>
    /// <response code="404">The farm, animal, or category was not found.</response>
    [HttpPost("image-check-in")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency key is required." });

        if (farmId == Guid.Empty)
            return BadRequest(new { error = "Farm id is required." });

        if (livestockAnimalId is null && livestockCategoryId is null)
            return BadRequest(new { error = "Provide either a livestock animal or a category." });

        using var imageStream = imageFile.OpenReadStream();
        var uploadResult = await _cloudinary.UploadImageAsync(imageStream, imageFile.FileName, "farmhand/poultry", ct);

        var visionResult = await _visionService.AnalyzeImageAsync(uploadResult.SecureUrl, farmerNotes ?? "", ct);

        var textForExtraction = string.IsNullOrWhiteSpace(farmerNotes)
            ? $"Image analysis: {visionResult.Description}. Issues: {string.Join(", ", visionResult.DetectedIssues)}"
            : farmerNotes;

        var extracted = await _aiService.ExtractObservationAsync(textForExtraction, ct);

        var visionAnalysis = System.Text.Json.JsonSerializer.Serialize(new
        {
            visionResult.Description,
            visionResult.DetectedIssues,
            visionResult.Confidence,
            AnalyzedAt = DateTime.UtcNow
        });

        var command = new RecordObservationCommand(
            farmId,
            livestockAnimalId,
            livestockCategoryId,
            textForExtraction,
            idempotencyKey,
            uploadResult.SecureUrl,
            extracted,
            visionAnalysis);

        var observationId = await _mediator.Send(command, ct);

        return Accepted(new
        {
            observationId,
            imageUrl = uploadResult.SecureUrl,
            visionAnalysis = new
            {
                visionResult.Description,
                visionResult.DetectedIssues,
                visionResult.Confidence
            },
            extracted = new
            {
                extracted.MortalityCount,
                extracted.WaterConsumption,
                extracted.FeedConsumption,
                extracted.EggProduction,
                extracted.FarmerNotes
            }
        });
    }

    /// <summary>
    /// Voice response: converts vet instructions text to audio via YarnGPT TTS (Pidgin/Yoruba support).
    /// </summary>
    /// <param name="request">Text to speak, optional voice, and optional target language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Synthesized audio URL and credits charged.</response>
    /// <response code="400">Request body missing or invalid.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="502">Speech synthesis provider failed.</response>
    [HttpPost("respond")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> VoiceRespond(
        [FromBody] VoiceRespondRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text is required." });

        try
        {
            var ttsResult = await _yarnGpt.SynthesizeSpeechAsync(
                request.Text,
                request.Voice ?? "umar",
                outputFormat: "mp3",
                targetLanguage: request.TargetLanguage,
                ct);

            return Ok(new
            {
                audioUrl = ttsResult.AudioUrl,
                creditsCharged = ttsResult.CreditsCharged
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice response TTS failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Speech synthesis is unavailable." });
        }
    }

    /// <summary>List available YarnGPT voices.</summary>
    /// <response code="200">Array of voice ids.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="502">Voice provider failed.</response>
    [HttpGet("voices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetVoices(CancellationToken ct)
    {
        try
        {
            var voices = await _yarnGpt.GetVoicesAsync(ct);
            return Ok(voices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch voices");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Voice service is unavailable." });
        }
    }
}

/// <summary>Payload for text-to-speech synthesis.</summary>
/// <param name="Text">Text to convert to speech.</param>
/// <param name="Voice">Optional voice id; defaults to "umar" server-side.</param>
/// <param name="TargetLanguage">Optional target language, for example "yoruba" or "pidgin".</param>
public record VoiceRespondRequest(string Text, string? Voice, string? TargetLanguage);
