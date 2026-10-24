namespace FarmHand.Application.Common.Interfaces;

public record TtsResult(string AudioUrl, string Provider);

/// <summary>Text-to-speech synthesis for generating voice responses to farmers.</summary>
public interface ITtsService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Synthesizes speech from text. Returns null on failure instead of throwing so callers can
    /// treat TTS as best-effort and always fall back to the text response.
    /// </summary>
    Task<TtsResult?> SynthesizeAsync(string text, string? language, CancellationToken ct = default);
}
