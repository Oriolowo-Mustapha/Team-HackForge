using FarmHand.Application.Common.Interfaces;
using FarmHand.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace FarmHand.Infrastructure.AI.Services;

/// <summary>
/// Text-to-speech: Spitch first (African languages), YarnGPT as fallback.
/// Never throws — returns null when TTS is unavailable so callers can degrade gracefully.
/// </summary>
public class TtsService : ITtsService
{
    private readonly SpitchService _spitch;
    private readonly YarnGptService _yarnGpt;
    private readonly ILogger<TtsService> _logger;

    public TtsService(SpitchService spitch, YarnGptService yarnGpt, ILogger<TtsService> logger)
    {
        _spitch = spitch;
        _yarnGpt = yarnGpt;
        _logger = logger;
    }

    public bool IsConfigured => _spitch.IsConfigured || _yarnGpt.IsConfigured;

    public async Task<TtsResult?> SynthesizeAsync(string text, string? language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Trim to the TTS limit
        var trimmed = text.Length > 1500 ? text[..1500] : text;

        if (_spitch.IsConfigured)
        {
            try
            {
                var voice = PickSpitchVoice(language);
                var result = await _spitch.SynthesizeSpeechAsync(trimmed, voice, language, speed: 1.0m, format: "mp3", ct: ct);
                return new TtsResult(result.AudioUrl, "spitch");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Spitch TTS failed in TtsService, trying YarnGPT fallback");
            }
        }

        if (_yarnGpt.IsConfigured)
        {
            try
            {
                var result = await _yarnGpt.SynthesizeSpeechAsync(trimmed, "umar", outputFormat: "mp3", targetLanguage: language, ct: ct);
                return new TtsResult(result.AudioUrl, "yarngpt");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "YarnGPT TTS failed in TtsService");
            }
        }

        return null;
    }

    /// <summary>Picks the best Spitch voice for a given language code.</summary>
    private static string PickSpitchVoice(string? language) =>
        language?.Trim().ToLowerInvariant() switch
        {
            "yo" => "sade",
            "ha" => "umar",
            "ig" => "chiamaka",
            "pcm" => "amina",
            _ => "amina" // default Nigerian Pidgin voice
        };
}
