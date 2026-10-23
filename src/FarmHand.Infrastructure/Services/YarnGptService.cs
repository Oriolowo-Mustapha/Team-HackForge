using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Services;

public record YarnGptSttResult(string Transcript, string? Translation, decimal CreditsCharged);
public record YarnGptTtsResult(string AudioUrl, decimal CreditsCharged);

public class YarnGptService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<YarnGptService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public YarnGptService(HttpClient httpClient, IConfiguration configuration, ILogger<YarnGptService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["YarnGpt:ApiKey"] ?? string.Empty;
        _httpClient.BaseAddress ??= new Uri("https://api.yarngpt.ai/");
        if (IsConfigured)
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");
        _logger = logger;
    }

    public bool IsConfigured => FarmHand.Infrastructure.AI.Clients.GeminiAiClient.IsRealKey(_apiKey);

    public async Task<YarnGptSttResult> TranscribeAudioAsync(Stream audioStream, string fileName, string contentType, string idempotencyKey, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(idempotencyKey), "Idempotency-Key");

        var response = await _httpClient.PostAsync("/api/v1/asr", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YarnGPT STT upload failed: {StatusCode} {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"YarnGPT STT failed: {response.StatusCode}");
        }

        using var uploadDoc = JsonDocument.Parse(responseBody);
        var jobId = uploadDoc.RootElement.GetProperty("job_id").GetString()!;

        // Poll for completion
        var transcript = await PollSttStatusAsync(jobId, ct);
        var creditsCharged = uploadDoc.RootElement.TryGetProperty("credits_reserved", out var cr) ? cr.GetDecimal() : 0;

        return new YarnGptSttResult(transcript, null, creditsCharged);
    }

    private async Task<string> PollSttStatusAsync(string jobId, CancellationToken ct)
    {
        var pollUrl = $"/api/v1/asr/{jobId}";
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1500, ct);
            var response = await _httpClient.GetAsync(pollUrl, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString()!;

            if (status == "completed")
            {
                if (doc.RootElement.TryGetProperty("transcript", out var transcriptEl))
                    return transcriptEl.GetString() ?? string.Empty;
                return string.Empty;
            }

            if (status == "failed")
            {
                var errorMsg = doc.RootElement.TryGetProperty("error_message", out var em) ? em.GetString() ?? "Transcription failed" : "Transcription failed";
                throw new InvalidOperationException($"YarnGPT STT failed: {errorMsg}");
            }
        }

        throw new InvalidOperationException("YarnGPT STT timed out.");
    }

    public async Task<YarnGptTtsResult> SynthesizeSpeechAsync(string text, string voice, string outputFormat = "mp3", string? targetLanguage = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>
        {
            ["text"] = text,
            ["output_format"] = outputFormat
        };

        if (!string.IsNullOrWhiteSpace(voice))
            body["voice"] = voice;

        if (!string.IsNullOrWhiteSpace(targetLanguage))
            body["target_language"] = targetLanguage;

        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/v1/tts", content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YarnGPT TTS failed: {StatusCode} {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"YarnGPT TTS failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var jobId = doc.RootElement.GetProperty("job_id").GetString()!;

        // Poll for audio URL
        var audioUrl = await PollTtsStatusAsync(jobId, ct);

        return new YarnGptTtsResult(audioUrl, 0);
    }

    private async Task<string> PollTtsStatusAsync(string jobId, CancellationToken ct)
    {
        var pollUrl = $"/api/v1/status/{jobId}";
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(1000, ct);
            var response = await _httpClient.GetAsync(pollUrl, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetString()!;

            if (status == "completed")
            {
                if (doc.RootElement.TryGetProperty("audio_url", out var urlEl))
                    return urlEl.GetString() ?? string.Empty;
                throw new InvalidOperationException("TTS completed but no audio URL returned.");
            }

            if (status == "failed")
            {
                var userMsg = doc.RootElement.TryGetProperty("user_message", out var um) ? um.GetString() ?? "Synthesis failed" : "Synthesis failed";
                throw new InvalidOperationException($"YarnGPT TTS failed: {userMsg}");
            }
        }

        throw new InvalidOperationException("YarnGPT TTS timed out.");
    }

    public async Task<IEnumerable<string>> GetVoicesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/voices", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var voices = new List<string>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.TryGetProperty("id", out var idEl))
                voices.Add(idEl.GetString()!);
        }
        return voices;
    }
}
