namespace FarmHand.Infrastructure.AI.Clients;

public interface IGroqAiClient
{
    bool IsConfigured { get; }
    bool IsVisionConfigured { get; }
    string Model { get; }
    string? VisionModel { get; }

    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
    Task<string> CompleteWithImageAsync(string prompt, byte[] image, string mimeType, CancellationToken ct = default);
}
