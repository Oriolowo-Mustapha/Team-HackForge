namespace FarmHand.Infrastructure.AI.Clients;

public interface IGeminiAiClient
{
    Task<string> ExtractObservationRawAsync(string rawText, CancellationToken ct = default);
}
