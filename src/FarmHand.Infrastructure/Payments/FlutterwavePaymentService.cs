using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Payments;

public class FlutterwavePaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string? _publicKey;
    private readonly string _signingSecret;
    private readonly string _baseUrl;
    private readonly string _redirectUrl;
    private readonly bool _liveMode;
    private readonly ILogger<FlutterwavePaymentService> _logger;

    public FlutterwavePaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FlutterwavePaymentService> logger)
    {
        _httpClient = httpClient;
        _secretKey = configuration["Flutterwave:SecretKey"] ?? string.Empty;
        _publicKey = configuration["Flutterwave:PublicKey"];
        _signingSecret = configuration["Flutterwave:SigningSecret"]
            ?? configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Flutterwave:SigningSecret (or Jwt:SecretKey) is required.");
        _baseUrl = configuration["Flutterwave:BaseUrl"] ?? "https://api.flutterwave.com/v3";
        _redirectUrl = configuration["Flutterwave:RedirectUrl"] ?? string.Empty;
        _liveMode = string.Equals(configuration["Flutterwave:Mode"], "Live", StringComparison.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<PaymentResult> InitiatePaymentAsync(
        Guid vetRequestId,
        decimal amount,
        string currency,
        string customerEmail,
        string? customerName = null,
        CancellationToken ct = default)
    {
        if (amount <= 0)
            return new PaymentResult(false, null, "Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(customerEmail))
            return new PaymentResult(false, null, "A customer email is required to initialize a Flutterwave payment.");

        if (!_liveMode)
            return new PaymentResult(true, CreateSignedReference(vetRequestId), null, null, amount, currency);

        if (string.IsNullOrWhiteSpace(_secretKey))
            return new PaymentResult(false, null, "Payment provider is not configured.");

        try
        {
            var reference = $"FH_{vetRequestId:N}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/payments");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    tx_ref = reference,
                    amount,
                    currency = currency.Trim().ToUpperInvariant(),
                    redirect_url = _redirectUrl,
                    customer = new
                    {
                        email = customerEmail.Trim(),
                        name = customerName ?? string.Empty
                    },
                    customizations = new
                    {
                        title = "FarmHand",
                        description = "Vet request payment",
                        logo = string.Empty
                    }
                }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flutterwave initiation failed: {StatusCode} {Body}", response.StatusCode, body);
                return new PaymentResult(false, null, "Payment provider rejected the initiation request.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string? checkoutUrl = null;
            string? flutterwaveId = null;

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("link", out var linkEl))
                    checkoutUrl = linkEl.GetString();
                if (data.TryGetProperty("id", out var idEl))
                    flutterwaveId = idEl.GetString();
                if (data.TryGetProperty("tx_ref", out var txRefEl))
                    reference = txRefEl.GetString() ?? reference;
            }

            if (string.IsNullOrWhiteSpace(flutterwaveId))
                return new PaymentResult(false, null, "Payment provider did not return a transaction id.");

            return new PaymentResult(true, flutterwaveId, null, checkoutUrl, amount, currency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flutterwave initiation error");
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

        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            _logger.LogWarning("Flutterwave secret key not configured - payment verification cannot run");
            return new PaymentResult(false, null, "Payment provider is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl.TrimEnd('/')}/transactions/{transactionReference}/verify");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flutterwave verification failed: {StatusCode} {Body}", response.StatusCode, responseBody);
                return new PaymentResult(false, null, "Payment verification failed.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var data = root.TryGetProperty("data", out var dataEl) ? dataEl : root;

            var status = data.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? string.Empty
                : string.Empty;
            var isPaid = string.Equals(status, "successful", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

            if (!isPaid)
                return new PaymentResult(false, transactionReference, "Payment has not been completed.");

            decimal? verifiedAmount = data.TryGetProperty("amount", out var amountEl) && amountEl.TryGetDecimal(out var amount)
                ? amount
                : null;
            string? verifiedCurrency = data.TryGetProperty("currency", out var currencyEl)
                ? currencyEl.GetString()
                : null;

            return new PaymentResult(true, transactionReference, null, null, verifiedAmount, verifiedCurrency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flutterwave verification error");
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

    public bool VerifyWebhookSignature(string rawBody, string? flutterwaveSignature, string? verifHash, string secretHash)
    {
        if (!string.IsNullOrWhiteSpace(flutterwaveSignature))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretHash));
            var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hash),
                Encoding.UTF8.GetBytes(flutterwaveSignature));
        }

        if (!string.IsNullOrWhiteSpace(verifHash))
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(secretHash),
                Encoding.UTF8.GetBytes(verifHash));
        }

        return false;
    }
}
