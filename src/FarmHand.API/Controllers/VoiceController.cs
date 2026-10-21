using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Poultry;
using FarmHand.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [HttpPost("check-in")]
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
            var command = new RecordObservationCommand(
                flockId,
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
    [HttpPost("image-check-in")]
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
            var command = new RecordObservationCommand(
                flockId,
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
    [HttpPost("respond")]
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

    [HttpGet("voices")]
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

public record VoiceRespondRequest(string Text, string? Voice, string? TargetLanguage);
