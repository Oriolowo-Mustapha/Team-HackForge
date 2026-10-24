using FarmHand.Infrastructure.AI.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Services;

public record SpitchTranscriptionResult(string RequestId, string Text, string? DetectedLanguage);
public record SpitchTtsResult(byte[] AudioBytes, string ContentType, string AudioUrl);
public record SpitchVoice(string Id, string Name, string Language, string Gender, string Accent);

/// <summary>
/// Service client for Spitch Speech-to-Text (transcriptions) and Text-to-Speech (synthesis).
/// Supports African languages (Yoruba, Hausa, Igbo, Nigerian Pidgin) and English.
/// </summary>
public class SpitchService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<SpitchService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SpitchService(HttpClient httpClient, IConfiguration configuration, ILogger<SpitchService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Spitch:ApiKey"]
            ?? configuration["Spitch__ApiKey"]
            ?? string.Empty;

        _httpClient.BaseAddress ??= new Uri("https://api.spitch.app/");
        if (IsConfigured)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        _logger = logger;
    }

    public bool IsConfigured => GeminiAiClient.IsRealKey(_apiKey);

    /// <summary>
    /// Transcribes an audio recording using Spitch Speech to Text (/v1/transcriptions).
    /// </summary>
    public async Task<SpitchTranscriptionResult> TranscribeAudioAsync(
        Stream audioStream,
        string fileName,
        string contentType,
        string? language = null,
        string timestamp = "none",
        string? specialWords = null,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "content", fileName);
        form.Add(new StringContent(timestamp), "timestamp");

        if (!string.IsNullOrWhiteSpace(language))
        {
            form.Add(new StringContent(language.Trim().ToLowerInvariant()), "language");
        }

        if (!string.IsNullOrWhiteSpace(specialWords))
        {
            form.Add(new StringContent(specialWords.Trim()), "special_words");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/transcriptions")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Spitch STT transcription failed with {StatusCode}", response.StatusCode);
            throw new HttpRequestException("Spitch transcription is unavailable.", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var requestId = root.TryGetProperty("request_id", out var reqEl) ? reqEl.GetString() ?? string.Empty : string.Empty;
        var text = root.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? string.Empty : string.Empty;
        var detectedLanguage = root.TryGetProperty("detected_language", out var langEl) ? langEl.GetString() : null;

        return new SpitchTranscriptionResult(requestId, text, detectedLanguage);
    }

    /// <summary>
    /// Synthesizes text into spoken audio bytes using Spitch Speech Generation (/v1/speech).
    /// </summary>
    public async Task<SpitchTtsResult> SynthesizeSpeechAsync(
        string text,
        string voice,
        string? language = null,
        decimal speed = 1.0m,
        string format = "mp3",
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["text"] = text,
            ["voice"] = voice,
            ["speed"] = Math.Clamp(speed, 0.7m, 1.2m),
            ["format"] = format
        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            payload["language"] = language.Trim().ToLowerInvariant();
        }

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/speech")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Spitch TTS failed with {StatusCode}", response.StatusCode);
            throw new HttpRequestException("Spitch speech synthesis is unavailable.", null, response.StatusCode);
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = format == "wav" ? "audio/wav" : "audio/mpeg";
        var base64 = Convert.ToBase64String(audioBytes);
        var dataUri = $"data:{contentType};base64,{base64}";

        return new SpitchTtsResult(audioBytes, contentType, dataUri);
    }

    /// <summary>
    /// Returns default supported voices across Yoruba, Hausa, Igbo, Pidgin and English.
    /// </summary>
    public Task<IReadOnlyList<SpitchVoice>> GetVoicesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<SpitchVoice> voices =
        [
            new("sade", "Sade", "yo", "Female", "Yoruba"),
            new("femi", "Femi", "yo", "Male", "Yoruba"),
            new("umar", "Umar", "ha", "Male", "Hausa"),
            new("zainab", "Zainab", "ha", "Female", "Hausa"),
            new("chiamaka", "Chiamaka", "ig", "Female", "Igbo"),
            new("ebuka", "Ebuka", "ig", "Male", "Igbo"),
            new("amina", "Amina", "pcm", "Female", "Nigerian Pidgin"),
            new("segun", "Segun", "pcm", "Male", "Nigerian Pidgin")
        ];

        return Task.FromResult(voices);
    }
}
