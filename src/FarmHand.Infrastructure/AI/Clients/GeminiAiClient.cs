using System.Text;
using System.Text.Json;
using FarmHand.Application.Common.Interfaces;

namespace FarmHand.Infrastructure.AI.Clients;

public class GeminiAiClient : IGeminiAiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiAiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    }

    public async Task<string> ExtractObservationRawAsync(string rawText, CancellationToken ct = default)
    {
        var prompt = $@"
Extract structured poultry farm observations from this farmer message: ""{rawText}"".
Return JSON only with these fields: mortalityCount (int), waterConsumption (string or null), feedConsumption (string or null), eggProduction (string or null), farmerNotes (string or null).
Do not include any text outside the JSON.";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return text;
    }
}
