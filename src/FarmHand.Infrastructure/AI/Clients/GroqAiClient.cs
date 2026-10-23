using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.AI.Clients;

/// <summary>
/// Groq OpenAI-compatible chat client used as the text fallback. Vision is only used when
/// <c>Groq:VisionModel</c> is configured, because Groq's vision catalogue changes frequently.
/// </summary>
public class GroqAiClient : IGroqAiClient
{
    public const string DefaultModel = "openai/gpt-oss-120b";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GroqAiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Groq:ApiKey"] ?? string.Empty;
        Model = GeminiAiClient.NonEmpty(configuration["Groq:Model"]) ?? DefaultModel;
        VisionModel = GeminiAiClient.NonEmpty(configuration["Groq:VisionModel"]);
        _httpClient.BaseAddress ??= new Uri(GeminiAiClient.NonEmpty(configuration["Groq:BaseUrl"]) ?? "https://api.groq.com/");
    }

    public bool IsConfigured => GeminiAiClient.IsRealKey(_apiKey);
    public bool IsVisionConfigured => IsConfigured && VisionModel is not null;
    public string Model { get; }
    public string? VisionModel { get; }

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default) =>
        SendAsync(Model, prompt, ct);

    public Task<string> CompleteWithImageAsync(string prompt, byte[] image, string mimeType, CancellationToken ct = default)
    {
        if (VisionModel is null)
            throw new AiProviderException("No Groq vision model is configured.");

        object content = new object[]
        {
            new { type = "text", text = prompt },
            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{Convert.ToBase64String(image)}" } }
        };
        return SendAsync(VisionModel, content, ct);
    }

    private async Task<string> SendAsync(string model, object content, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new AiProviderException("Groq is not configured.");

        var body = new
        {
            model,
            messages = new[] { new { role = "user", content } },
            temperature = 0.2
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "openai/v1/chat/completions");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Groq returned {(int)response.StatusCode} for model {model}.", null, response.StatusCode);

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new AiProviderException("Groq returned no choices.");

        var text = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new AiProviderException("Groq returned no text.");
        return text;
    }
}
