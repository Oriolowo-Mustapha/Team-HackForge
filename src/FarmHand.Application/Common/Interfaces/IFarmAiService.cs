namespace FarmHand.Application.Common.Interfaces;

public record ExtractedObservation(
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes);

public interface IFarmAiService
{
    Task<ExtractedObservation> ExtractObservationAsync(string rawText, CancellationToken ct = default);
}
