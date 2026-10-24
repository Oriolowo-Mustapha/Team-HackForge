using FarmHand.Application.Common.Exceptions;
using FarmHand.Infrastructure.AI.Clients;
using FarmHand.Infrastructure.Services;

namespace FarmHand.Infrastructure.AI.Services;

/// <summary>Voice-to-text: Spitch (African and English languages) first, YarnGPT second, Gemini audio understanding third.</summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly SpitchService _spitch;
    private readonly YarnGptService _yarnGpt;
    private readonly IGeminiAiClient _gemini;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(
        SpitchService spitch,
        YarnGptService yarnGpt,
        IGeminiAiClient gemini,
        ILogger<TranscriptionService> logger)
    {
        _spitch = spitch;
        _yarnGpt = yarnGpt;
        _gemini = gemini;
        _logger = logger;
    }

    public bool IsConfigured => _spitch.IsConfigured || _yarnGpt.IsConfigured || _gemini.IsConfigured;

    public async Task<TranscriptionResult> TranscribeAsync(byte[] audio, string contentType, string fileName, string idempotencyKey, CancellationToken ct = default)
    {
        if (_spitch.IsConfigured)
        {
            try
            {
                using var stream = new MemoryStream(audio);
                var result = await _spitch.TranscribeAudioAsync(stream, fileName, contentType, ct: ct);
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    _logger.LogInformation("Audio successfully transcribed with Spitch (RequestId: {RequestId}, DetectedLang: {Lang})",
                        result.RequestId, result.DetectedLanguage);
                    return new TranscriptionResult(result.Text.Trim(), "spitch", result.DetectedLanguage);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Spitch transcription failed, trying fallback providers");
            }
        }

        if (_yarnGpt.IsConfigured)
        {
            try
            {
                using var stream = new MemoryStream(audio);
                var result = await _yarnGpt.TranscribeAudioAsync(stream, fileName, contentType, idempotencyKey, ct);
                if (!string.IsNullOrWhiteSpace(result.Transcript))
                    return new TranscriptionResult(result.Transcript.Trim(), "yarngpt");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "YarnGPT transcription failed, trying Gemini fallback");
            }
        }

        if (_gemini.IsConfigured)
        {
            try
            {
                const string prompt = "Transcribe this voice note from a farmer exactly as spoken. If it is in Nigerian Pidgin, Yoruba, Hausa or Igbo, transcribe it and then add an English translation after ' | English: '. Return only the transcript text.";
                var transcript = await _gemini.GenerateWithMediaAsync(prompt, audio, contentType, json: false, ct);
                if (!string.IsNullOrWhiteSpace(transcript))
                    return new TranscriptionResult(AIInputGuard.Sanitize(transcript), $"gemini:{_gemini.VisionModel}");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Gemini transcription failed");
            }
        }

        throw new AiUnavailableException(IsConfigured
            ? "Your voice note could not be transcribed right now. It is still on your device: type what you said or try again."
            : "Voice transcription is not configured on this server. Type what you observed instead.");
    }
}
