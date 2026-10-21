using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Poultry;
using FarmHand.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;

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
    private readonly YarnGptService _yarnGpt;
    private readonly ICloudinaryService _cloudinary;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(
        IMediator mediator,
        IFarmAiService aiService,
        YarnGptService yarnGpt,
        ICloudinaryService cloudinary,
        ILogger<VoiceController> logger)
    {
        _mediator = mediator;
        _aiService = aiService;
        _yarnGpt = yarnGpt;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    /// <summary>
    /// Voice check-in: uploads audio, transcribes via YarnGPT STT, extracts structured observation via Gemini.
    /// </summary>
    /// <remarks>
    /// Send multipart/form-data with the audio in <c>audioFile</c>; pass <c>flockId</c> and
    /// <c>idempotencyKey</c> as query-string parameters. The idempotency key is not yet
    /// used for de-duplication.
    /// </remarks>
    /// <param name="audioFile">Audio recording (webm/mp3/wav).</param>
    /// <param name="flockId">Flock the observation belongs to.</param>
    /// <param name="idempotencyKey">Client-generated key for retry safety.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Observation created from the transcript.</response>
    /// <response code="400">Missing audio file or idempotency key.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="500">STT, AI extraction, or persistence failed.</response>
    [HttpPost("check-in")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VoiceCheckIn(
        IFormFile? audioFile,
        Guid flockId,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (audioFile == null || audioFile.Length == 0)
            return BadRequest(new { error = "No audio file provided." });

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency key is required." });

        try
        {
            // 1. Transcribe audio via YarnGPT STT
            using var audioStream = audioFile.OpenReadStream();
            var sttResult = await _yarnGpt.TranscribeAudioAsync(audioStream, audioFile.FileName, idempotencyKey, ct);
            _logger.LogInformation("YarnGPT STT completed for voice check-in");

            // 2. Extract structured observation via Gemini (once)
            var extracted = await _aiService.ExtractObservationAsync(sttResult.Transcript, ct);

            // 3. Record the observation with pre-extracted data (avoids double AI call)
            // Voice check-in is flock-level (category-level observation)
            var command = new RecordObservationCommand(
                Guid.Empty, // flock-level, no specific animal
                null, // will be set by caller or use default poultry category
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice check-in failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Image check-in: uploads image to Cloudinary, analyzes via Groq Vision, extracts via Gemini.
    /// </summary>
    /// <remarks>
    /// Send multipart/form-data with the image in <c>imageFile</c>; pass <c>flockId</c>,
    /// <c>idempotencyKey</c>, and optional <c>farmerNotes</c> as query-string parameters.
    /// Cloudinary uploads are currently unsigned and will be rejected until request signing is added.
    /// </remarks>
    /// <param name="imageFile">Image of the flock or environment.</param>
    /// <param name="flockId">Flock the observation belongs to.</param>
    /// <param name="idempotencyKey">Client-generated key for retry safety.</param>
    /// <param name="farmerNotes">Optional notes to combine with vision output.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Observation created from the image analysis.</response>
    /// <response code="400">Missing image file or idempotency key.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="500">Upload, vision analysis, AI extraction, or persistence failed.</response>
    [HttpPost("image-check-in")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ImageCheckIn(
        IFormFile imageFile,
        Guid flockId,
        string idempotencyKey,
        string? farmerNotes = null,
        CancellationToken ct = default)
    {
        if (imageFile == null || imageFile.Length == 0)
            return BadRequest(new { error = "No image file provided." });

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency key is required." });

        try
        {
            // 1. Upload image to Cloudinary
            using var imageStream = imageFile.OpenReadStream();
            var uploadResult = await _cloudinary.UploadImageAsync(imageStream, imageFile.FileName, "farmhand/poultry", ct);

            // 2. Analyze image via Groq Vision (once)
            var visionResult = await AnalyzeImageWithGroq(uploadResult.SecureUrl, farmerNotes ?? "", ct);

            // 3. Build text for Gemini extraction from vision + notes
            var textForExtraction = string.IsNullOrWhiteSpace(farmerNotes)
                ? $"Image analysis: {visionResult.Description}. Issues: {string.Join(", ", visionResult.DetectedIssues)}"
                : farmerNotes;

            // 4. Extract structured observation via Gemini (once)
            var extracted = await _aiService.ExtractObservationAsync(textForExtraction, ct);

            // 5. Serialize vision analysis for storage
            var visionAnalysis = System.Text.Json.JsonSerializer.Serialize(new
            {
                visionResult.Description,
                visionResult.DetectedIssues,
                visionResult.Confidence,
                AnalyzedAt = DateTime.UtcNow
            });

            // 6. Record the observation with pre-extracted data and pre-analyzed vision
            // Image check-in is flock-level (category-level observation)
            var command = new RecordObservationCommand(
                Guid.Empty, // flock-level
                null,
                textForExtraction,
                idempotencyKey,
                uploadResult.SecureUrl,
                extracted,
                visionAnalysis);

            var observationId = await _mediator.Send(command, ct);

            // Override vision analysis with the one we already computed
            // (the command will re-run vision if ImageUrl is set, so we pass it via a different path)
            // Since command re-runs vision when ImageUrl is provided, we need to prevent that.
            // Instead, pass ImageUrl but the vision analysis will be overwritten below.
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image check-in failed");
            return StatusCode(500, new { error = "Image check-in failed. Please try again." });
        }
    }

    private async Task<VisionAnalysisResultDto> AnalyzeImageWithGroq(string imageUrl, string farmerNotes, CancellationToken ct)
    {
        // Direct Groq vision call for real-time response in controller
        var prompt = $@"
Analyze this farm image and the farmer's notes: ""{farmerNotes}"".
Return JSON only with these fields:
- description: A clear description of what is visible
- detected_issues: Array of concerning issues
- confidence: 0.0 to 1.0
No diagnosis or treatment. Only describe visible concerns for veterinary review.";

        var requestBody = new
        {
            model = "llama-3.2-11b-vision-preview",
messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = imageUrl } }
                    }
                }
            },
            temperature = 0.1,
            max_tokens = 1000
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var apiKey = Environment.GetEnvironmentVariable("YARN_GPT_API_KEY") ?? "";
        // Use Groq API key for vision
        var groqApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqApiKey}");

        var response = await httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Groq vision failed: {StatusCode} {Body}", response.StatusCode, responseBody);
            return new VisionAnalysisResultDto("Vision analysis unavailable", Array.Empty<string>(), 0);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        var cleaned = CleanJsonResponse(text);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<VisionAnalysisDto>(cleaned, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new VisionAnalysisResultDto(
            parsed?.Description ?? string.Empty,
            parsed?.DetectedIssues ?? Array.Empty<string>(),
            parsed?.Confidence ?? 0);
    }

    private static string CleanJsonResponse(string raw)
    {
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"\{.*\}", System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Value : raw;
    }

    private class VisionAnalysisDto
    {
        public string? Description { get; set; }
        public string[]? DetectedIssues { get; set; }
        public decimal Confidence { get; set; }
    }

    public record VisionAnalysisResultDto(string Description, string[] DetectedIssues, decimal Confidence);

    /// <summary>
    /// Voice response: converts vet instructions text to audio via YarnGPT TTS (Pidgin/Yoruba support).
    /// </summary>
    /// <param name="request">Text to speak, optional voice, and optional target language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Synthesized audio URL and credits charged.</response>
    /// <response code="400">Request body missing or invalid.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="500">TTS synthesis failed.</response>
    [HttpPost("respond")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VoiceRespond(
        [FromBody] VoiceRespondRequest request,
        CancellationToken ct)
    {
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
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>List available YarnGPT voices.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Array of voice ids.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="500">YarnGPT voice lookup failed.</response>
    [HttpGet("voices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>Payload for text-to-speech synthesis.</summary>
/// <param name="Text">Text to convert to speech.</param>
/// <param name="Voice">Optional voice id; defaults to "umar" server-side.</param>
/// <param name="TargetLanguage">Optional target language, for example "yoruba" or "pidgin".</param>
public record VoiceRespondRequest(string Text, string? Voice, string? TargetLanguage);
