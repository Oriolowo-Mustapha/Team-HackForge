namespace FarmHand.Infrastructure.AI.Clients;

public interface IGroqAiClient
{
    Task<string> ExtractObservationRawAsync(string rawText, CancellationToken ct = default);
}
