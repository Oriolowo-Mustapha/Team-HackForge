using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Payments;

public class NombaPaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _signingSecret;
    private readonly string _baseUrl;
    private readonly bool _liveMode;
    private readonly ILogger<NombaPaymentService> _logger;

    public NombaPaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NombaPaymentService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Nomba:ApiKey"] ?? string.Empty;
        _signingSecret = configuration["Nomba:SigningSecret"]
            ?? configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Nomba:SigningSecret (or Jwt:SecretKey) is required.");
        _baseUrl = configuration["Nomba:BaseUrl"] ?? "https://sandbox.nomba.com";
        _liveMode = string.Equals(configuration["Nomba:Mode"], "Live", StringComparison.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<PaymentResult> InitiatePaymentAsync(Guid vetRequestId, decimal amount, string currency, CancellationToken ct = default)
    {
        if (amount <= 0)
            return new PaymentResult(false, null, "Amount must be greater than zero.");

        if (!_liveMode)
            return new PaymentResult(true, CreateSignedReference(vetRequestId), null, amount, currency);

        if (string.IsNullOrWhiteSpace(_apiKey))
            return new PaymentResult(false, null, "Payment provider is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/api/v1/payments/initiate");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    reference = $"FH_{vetRequestId:N}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                    amount,
                    currency
                }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nomba initiation failed: {StatusCode} {Body}", response.StatusCode, body);
                return new PaymentResult(false, null, "Payment provider rejected the initiation request.");
            }

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.TryGetProperty("data", out var dataEl) ? dataEl : doc.RootElement;
            string? reference = null;
            if (data.TryGetProperty("reference", out var refEl))
                reference = refEl.GetString();
            else if (data.TryGetProperty("transactionReference", out var txEl))
                reference = txEl.GetString();

            if (string.IsNullOrWhiteSpace(reference))
                return new PaymentResult(false, null, "Payment provider did not return a reference.");

            return new PaymentResult(true, reference, null, amount, currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nomba initiation error");
            return new PaymentResult(false, null, "Payment provider is unavailable.");
        }
    }

    public async Task<PaymentResult> VerifyPaymentAsync(string transactionReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transactionReference))
            return new PaymentResult(false, null, "A transaction reference is required.");

        if (!_liveMode)
        {
            return IsValidSignedReference(transactionReference)
                ? new PaymentResult(true, transactionReference, null)
                : new PaymentResult(false, null, "Unknown or invalid payment reference.");
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Nomba API key not configured - payment verification cannot run");
            return new PaymentResult(false, null, "Payment provider is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/api/v1/payments/verify");
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
                return new PaymentResult(false, null, "Payment verification failed.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var data = doc.RootElement.TryGetProperty("data", out var dataEl) ? dataEl : doc.RootElement;

            var status = data.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? string.Empty
                : string.Empty;
            var isPaid = string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "successful", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

            if (!isPaid)
                return new PaymentResult(false, transactionReference, "Payment has not been completed.");

            decimal? verifiedAmount = data.TryGetProperty("amount", out var amountEl) && amountEl.TryGetDecimal(out var amount)
                ? amount
                : null;
            string? verifiedCurrency = data.TryGetProperty("currency", out var currencyEl)
                ? currencyEl.GetString()
                : null;

            return new PaymentResult(true, transactionReference, null, verifiedAmount, verifiedCurrency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nomba verification error");
            return new PaymentResult(false, null, "Payment provider is unavailable.");
        }
    }

    private string CreateSignedReference(Guid vetRequestId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var payload = $"{vetRequestId:N}.{timestamp}.{nonce}";
        return $"FH_{vetRequestId:N}_{timestamp}_{nonce}_{Sign(payload)}";
    }

    private bool IsValidSignedReference(string reference)
    {
        var parts = reference.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "FH") return false;

        var payload = $"{parts[1]}.{parts[2]}.{parts[3]}";
        var expected = Sign(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(parts[4]));
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()[..16];
    }
}
