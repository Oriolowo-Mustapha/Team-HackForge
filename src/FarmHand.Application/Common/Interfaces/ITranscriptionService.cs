namespace FarmHand.Application.Common.Interfaces;

public record TranscriptionResult(string Transcript, string Source);

/// <summary>Speech-to-text for voice reports (YarnGPT for Nigerian languages, with a Gemini fallback).</summary>
public interface ITranscriptionService
{
    bool IsConfigured { get; }

    /// <summary>Throws <see cref="Exceptions.AiUnavailableException"/> when no provider could transcribe the audio.</summary>
    Task<TranscriptionResult> TranscribeAsync(byte[] audio, string contentType, string fileName, string idempotencyKey, CancellationToken ct = default);
}
