using FarmHand.Application.Common.Interfaces;
using FarmHand.Infrastructure.AI.Clients;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FarmHand.Infrastructure.AI.Services;

public class FarmAiService : IFarmAiService
{
    private readonly IGeminiAiClient _geminiClient;
    private readonly IGroqAiClient _groqClient;
    private readonly ILogger<FarmAiService> _logger;

    public FarmAiService(
        IGeminiAiClient geminiClient,
        IGroqAiClient groqClient,
        ILogger<FarmAiService> logger)
    {
        _geminiClient = geminiClient;
        _groqClient = groqClient;
        _logger = logger;
    }

    public async Task<ExtractedObservation> ExtractObservationAsync(string rawText, CancellationToken ct = default)
    {
        string? rawJson = null;
        string? modelUsed = null;

        try
        {
            rawJson = await _geminiClient.ExtractObservationRawAsync(rawText, ct);
            modelUsed = "Gemini";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Gemini failed, falling back to Groq");
            try
            {
                rawJson = await _groqClient.ExtractObservationRawAsync(rawText, ct);
                modelUsed = "Groq";
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Both Gemini and Groq failed");
                throw new InvalidOperationException("AI extraction service unavailable", ex2);
            }
        }

        if (string.IsNullOrWhiteSpace(rawJson))
            throw new InvalidOperationException("AI returned empty response.");

        var cleaned = CleanJsonResponse(rawJson);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<AiObservationDto>(cleaned, options)
            ?? throw new InvalidOperationException("Failed to parse AI response.");

        return new ExtractedObservation(
            parsed.MortalityCount,
            parsed.WaterConsumption,
            parsed.FeedConsumption,
            parsed.EggProduction,
            parsed.FarmerNotes);
    }

    private static string CleanJsonResponse(string raw)
    {
        var match = Regex.Match(raw, @"\{.*\}", RegexOptions.Singleline);
        return match.Success ? match.Value : raw;
    }

    private class AiObservationDto
    {
        public int MortalityCount { get; set; }
        public string? WaterConsumption { get; set; }
        public string? FeedConsumption { get; set; }
        public string? EggProduction { get; set; }
        public string? FarmerNotes { get; set; }
    }
}
