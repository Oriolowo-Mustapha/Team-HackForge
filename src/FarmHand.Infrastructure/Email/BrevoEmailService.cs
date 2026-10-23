using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FarmHand.Infrastructure.Email;

/// <summary>
/// Brevo transactional email delivery via the v3 API.
/// Disabled by default; enable with the <c>Brevo</c> configuration section.
/// Credentials are never logged or returned to callers.
/// </summary>
public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly bool _enabled;
    private readonly string _apiKey;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _baseUrl;

    public BrevoEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _enabled = configuration.GetValue("Brevo:Enabled", false);
        _apiKey = configuration["Brevo:ApiKey"] ?? string.Empty;
        _fromAddress = configuration["Brevo:FromAddress"] ?? "no-reply@farmhand.ai";
        _fromName = configuration["Brevo:FromName"] ?? "FarmHand";
        _baseUrl = (configuration["Brevo:BaseUrl"] ?? "https://api.brevo.com/v3").TrimEnd('/');
    }

    public bool IsEnabled =>
        _enabled &&
        !string.IsNullOrWhiteSpace(_apiKey) &&
        !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation(
                "Email delivery is disabled; skipped sending {Subject} to {Recipient}",
                message.Subject,
                message.To);
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/smtp/email");
            request.Headers.TryAddWithoutValidation("api-key", _apiKey);
            request.Headers.TryAddWithoutValidation("accept", "application/json");

            var payload = new Dictionary<string, object?>
            {
                ["sender"] = new { name = _fromName, email = _fromAddress },
                ["to"] = new[] { new { email = message.To } },
                ["subject"] = message.Subject,
                ["htmlContent"] = message.HtmlBody
            };

            if (!string.IsNullOrWhiteSpace(message.TextBody))
                payload["textContent"] = message.TextBody;

            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return true;

            // Log only delivery metadata; never the message body (it can contain credentials).
            _logger.LogWarning(
                "Brevo email delivery failed with {StatusCode} for {Subject} to {Recipient}",
                response.StatusCode,
                message.Subject,
                message.To);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Brevo email delivery error for {Subject} to {Recipient}",
                message.Subject,
                message.To);
            return false;
        }
    }
}
