using FarmHand.Application.Common.Exceptions;
using FarmHand.Infrastructure.AI.Clients;
using FarmHand.Infrastructure.Services;

namespace FarmHand.Infrastructure.AI.Services;

/// <summary>Voice-to-text: YarnGPT (built for Nigerian languages) first, Gemini audio understanding second.</summary>
public class TranscriptionService : ITranscriptionService
{
    private readonly YarnGptService _yarnGpt;
    private readonly IGeminiAiClient _gemini;
    private readonly ILogger<TranscriptionService> _logger;

    public TranscriptionService(YarnGptService yarnGpt, IGeminiAiClient gemini, ILogger<TranscriptionService> logger)
    {
        _yarnGpt = yarnGpt;
        _gemini = gemini;
        _logger = logger;
    }

    public bool IsConfigured => _yarnGpt.IsConfigured || _gemini.IsConfigured;

    public async Task<TranscriptionResult> TranscribeAsync(byte[] audio, string contentType, string fileName, string idempotencyKey, CancellationToken ct = default)
    {
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
                _logger.LogWarning(ex, "YarnGPT transcription failed");
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
