namespace FarmHand.Infrastructure.AI.Clients;

/// <summary>Thrown when a provider answers but the answer is unusable (blocked, empty, malformed).</summary>
public class AiProviderException : Exception
{
    public AiProviderException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IGeminiAiClient
{
    bool IsConfigured { get; }
    string Model { get; }
    string VisionModel { get; }

    /// <summary>Text-only generation in JSON mode. Returns the raw model text.</summary>
    Task<string> GenerateJsonAsync(string prompt, CancellationToken ct = default);

    /// <summary>Multimodal generation (image or audio inline). Returns the raw model text.</summary>
    Task<string> GenerateWithMediaAsync(string prompt, byte[] media, string mimeType, bool json, CancellationToken ct = default);
}
