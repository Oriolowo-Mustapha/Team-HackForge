using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Payments;

public class NombaPaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<NombaPaymentService> _logger;

    public NombaPaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NombaPaymentService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Nomba:ApiKey"] ?? string.Empty;
        _baseUrl = configuration["Nomba:BaseUrl"] ?? "https://sandbox.nomba.com";
        _logger = logger;
    }

    public async Task<PaymentResult> InitiatePaymentAsync(Guid vetRequestId, decimal amount, string currency, CancellationToken ct = default)
    {
        // Sandbox mode: simulate payment initiation
        _logger.LogInformation("Nomba sandbox: initiating payment for VetRequest {VetRequestId}, amount {Amount} {Currency}", vetRequestId, amount, currency);

        var reference = $"NOMBA_{vetRequestId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        await Task.Delay(100, ct); // simulate network

        return new PaymentResult(true, reference, null);
    }

    public async Task<PaymentResult> VerifyPaymentAsync(string transactionReference, CancellationToken ct = default)
    {
        // Verify with Nomba sandbox before marking as paid
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Nomba API key not configured - payment verification skipped");
            return new PaymentResult(false, null, "Payment provider not configured");
        }

        try
        {
            var verifyUrl = $"{_baseUrl}/api/v1/payments/verify";
            using var request = new HttpRequestMessage(HttpMethod.Post, verifyUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { reference = transactionReference }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nomba verification failed: {StatusCode} {Body}", response.StatusCode, responseBody);
                return new PaymentResult(false, null, "Payment verification failed");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "" : "";
            var isPaid = string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

            return new PaymentResult(isPaid, transactionReference, isPaid ? null : "Payment not completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nomba verification error");
            return new PaymentResult(false, null, "Payment verification error");
        }
    }
}
