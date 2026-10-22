using System.Text;
using System.Text.Json;
using FarmHand.Application.Common.Interfaces;

namespace FarmHand.Infrastructure.AI.Clients;

public class GroqAiClient : IGroqAiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GroqAiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Groq:ApiKey"] ?? string.Empty;
        _httpClient.BaseAddress = new Uri("https://api.groq.com/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<string> ExtractObservationRawAsync(string rawText, CancellationToken ct = default)
    {
        var prompt = $@"
Extract structured poultry farm observations from this farmer message: ""{rawText}"".
Return JSON only with these fields: mortalityCount (int), waterConsumption (string or null), feedConsumption (string or null), eggProduction (string or null), farmerNotes (string or null).
Do not include any text outside the JSON.";

        var requestBody = new
        {
            model = "mixtral-8x7b-32768",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("openai/v1/chat/completions", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return text;
    }

    public async Task<string> AssessSeverityRawAsync(string prompt, CancellationToken ct = default)
    {
        var requestBody = new
        {
            model = "mixtral-8x7b-32768",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("openai/v1/chat/completions", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(responseJson);

        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return text;
    }
}
