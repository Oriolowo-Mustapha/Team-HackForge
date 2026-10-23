using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.AI.Clients;

/// <summary>
/// Google Gemini REST client. Model ids come from configuration (<c>Gemini:Model</c>, <c>Gemini:VisionModel</c>)
/// because Google retires model versions regularly. The API key is sent in the <c>x-goog-api-key</c>
/// header so it never appears in URLs or request logs.
/// </summary>
public class GeminiAiClient : IGeminiAiClient
{
    public const string DefaultModel = "gemini-3.5-flash";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiAiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        Model = NonEmpty(configuration["Gemini:Model"]) ?? DefaultModel;
        VisionModel = NonEmpty(configuration["Gemini:VisionModel"]) ?? Model;
        _httpClient.BaseAddress ??= new Uri(NonEmpty(configuration["Gemini:BaseUrl"]) ?? "https://generativelanguage.googleapis.com/");
    }

    public bool IsConfigured => IsRealKey(_apiKey);
    public string Model { get; }
    public string VisionModel { get; }

    public Task<string> GenerateJsonAsync(string prompt, CancellationToken ct = default) =>
        SendAsync(Model, [new { text = prompt }], json: true, ct);

    public Task<string> GenerateWithMediaAsync(string prompt, byte[] media, string mimeType, bool json, CancellationToken ct = default) =>
        SendAsync(
            VisionModel,
            [new { text = prompt }, new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(media) } }],
            json,
            ct);

    private async Task<string> SendAsync(string model, object[] parts, bool json, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new AiProviderException("Gemini is not configured.");

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { role = "user", parts } },
            ["generationConfig"] = json
                ? new { temperature = 0.2, responseMimeType = "application/json" }
                : (object)new { temperature = 0.2 }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini returned {(int)response.StatusCode} for model {model}.", null, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new AiProviderException("Gemini returned no candidates (the request may have been blocked).");

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var responseParts))
            throw new AiProviderException("Gemini returned an empty candidate.");

        var text = new StringBuilder();
        foreach (var part in responseParts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var t))
                text.Append(t.GetString());
        }

        var result = text.ToString();
        if (string.IsNullOrWhiteSpace(result))
            throw new AiProviderException("Gemini returned no text.");
        return result;
    }

    internal static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool IsRealKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && !key.StartsWith("your-", StringComparison.OrdinalIgnoreCase)
        && !key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
