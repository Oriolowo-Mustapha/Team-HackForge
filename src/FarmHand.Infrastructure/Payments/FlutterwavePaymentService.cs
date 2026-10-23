using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Payments;

/// <summary>
/// Flutterwave Standard integration.
/// <list type="bullet">
/// <item><b>Live</b>: <c>POST /v3/payments</c> with our own <c>tx_ref</c>; the farmer is redirected to the
/// hosted checkout; status is confirmed server-side with <c>GET /v3/transactions/verify_by_reference</c>
/// (or <c>/v3/transactions/{id}/verify</c> for webhooks). Amount and currency always come from the provider.</item>
/// <item><b>Simulated</b>: no money moves. References are HMAC-signed so only references this server
/// issued can be confirmed. Every simulated payment is persisted and shown as simulated.</item>
/// <item><b>Disabled</b>: the default when <c>Flutterwave:Mode</c> is not set; initiation is refused.</item>
/// </list>
/// </summary>
public class FlutterwavePaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly string _signingSecret;
    private readonly string _baseUrl;
    private readonly string _redirectUrl;
    private readonly ILogger<FlutterwavePaymentService> _logger;

    public FlutterwavePaymentService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FlutterwavePaymentService> logger)
    {
        _httpClient = httpClient;
        _secretKey = configuration["Flutterwave:SecretKey"] ?? string.Empty;
        _signingSecret = configuration["Flutterwave:SigningSecret"]
            ?? configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Flutterwave:SigningSecret (or Jwt:SecretKey) is required.");
        _baseUrl = (configuration["Flutterwave:BaseUrl"] ?? "https://api.flutterwave.com/v3").TrimEnd('/');
        var publicUrl = (configuration["App:PublicUrl"] ?? string.Empty).TrimEnd('/');
        _redirectUrl = configuration["Flutterwave:RedirectUrl"] is { Length: > 0 } configured && !configured.Contains("example.com", StringComparison.OrdinalIgnoreCase)
            ? configured
            : $"{publicUrl}/payments/return";
        _logger = logger;

        var mode = configuration["Flutterwave:Mode"];
        Mode = string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase) ? "Live"
            : string.Equals(mode, "Simulated", StringComparison.OrdinalIgnoreCase) ? "Simulated"
            : "Disabled";
    }

    public string Mode { get; }

    private bool LiveMode => Mode == "Live";

    public async Task<PaymentResult> InitiatePaymentAsync(
        Guid vetRequestId,
        decimal amount,
        string currency,
        string customerEmail,
        string? customerName = null,
        PaymentSplit? split = null,
        CancellationToken ct = default)
    {
        if (Mode == "Disabled")
            return new PaymentResult(false, null, "Payments are not configured on this server.");

        if (amount <= 0)
            return new PaymentResult(false, null, "Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(customerEmail))
            return new PaymentResult(false, null, "A customer email is required to initialize a payment.");

        var reference = CreateSignedReference(vetRequestId);

        if (!LiveMode)
            return new PaymentResult(true, reference, null, null, amount, currency);

        if (string.IsNullOrWhiteSpace(_secretKey))
            return new PaymentResult(false, null, "Payment provider is not configured.");

        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["tx_ref"] = reference,
                ["amount"] = amount,
                ["currency"] = currency.Trim().ToUpperInvariant(),
                ["redirect_url"] = _redirectUrl,
                ["customer"] = new { email = customerEmail.Trim(), name = customerName ?? string.Empty },
                ["customizations"] = new { title = "FarmHand", description = "Veterinary service payment" }
            };

            if (split is not null && !string.IsNullOrWhiteSpace(split.SubaccountId))
            {
                payload["subaccounts"] = new[]
                {
                    new
                    {
                        id = split.SubaccountId,
                        // The vet's subaccount receives the remainder after the platform's commission.
                        transaction_charge_type = "percentage",
                        transaction_charge = Math.Round(split.PlatformCommissionRate, 6)
                    }
                };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/payments");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flutterwave initiation failed with {StatusCode}", response.StatusCode);
                return new PaymentResult(false, null, "The payment provider rejected the checkout request.");
            }

            using var doc = JsonDocument.Parse(body);
            var link = doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("link", out var linkEl)
                ? linkEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(link))
                return new PaymentResult(false, null, "The payment provider did not return a checkout link.");

            return new PaymentResult(true, reference, null, link, amount, currency);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Flutterwave initiation error");
            return new PaymentResult(false, null, "The payment provider is unavailable.");
        }
    }

    public Task<PaymentVerification> VerifyByReferenceAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, null, reference, "A reference is required."));

        if (!IsValidSignedReference(reference))
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, null, reference, "Unknown payment reference."));

        if (Mode == "Simulated")
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.Successful, null, null, null, reference, "Simulated payment confirmed. No money was moved."));

        if (!LiveMode)
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.Error, null, null, null, reference, "Payments are not configured on this server."));

        return QueryAsync($"{_baseUrl}/transactions/verify_by_reference?tx_ref={Uri.EscapeDataString(reference)}", reference, ct);
    }

    public Task<PaymentVerification> VerifyByTransactionIdAsync(string transactionId, CancellationToken ct = default)
    {
        if (!LiveMode)
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.Error, null, null, null, null, "Transaction-id verification is only available in live mode."));

        if (string.IsNullOrWhiteSpace(transactionId) || !transactionId.All(char.IsDigit))
            return Task.FromResult(new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, null, null, "Invalid transaction id."));

        return QueryAsync($"{_baseUrl}/transactions/{transactionId}/verify", null, ct);
    }

    private async Task<PaymentVerification> QueryAsync(string url, string? expectedReference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
            return new PaymentVerification(PaymentVerificationOutcome.Error, null, null, null, expectedReference, "Payment provider is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);
            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest)
                return new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, null, expectedReference, "The provider has no completed checkout for this reference yet.");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flutterwave verification failed with {StatusCode}", response.StatusCode);
                return new PaymentVerification(PaymentVerificationOutcome.Error, null, null, null, expectedReference, "The payment provider could not be reached.");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, null, expectedReference, "The provider returned no transaction.");

            var status = ReadText(data, "status") ?? string.Empty;
            var txRef = ReadText(data, "tx_ref");
            var id = ReadText(data, "id");
            decimal? amount = data.TryGetProperty("amount", out var amountEl) && amountEl.TryGetDecimal(out var a) ? a : null;
            var currency = ReadText(data, "currency");

            if (expectedReference is not null && !string.Equals(txRef, expectedReference, StringComparison.Ordinal))
                return new PaymentVerification(PaymentVerificationOutcome.NotFound, null, null, id, txRef, "The provider returned a different reference.");

            var outcome = status.ToLowerInvariant() switch
            {
                "successful" => PaymentVerificationOutcome.Successful,
                "failed" or "cancelled" => PaymentVerificationOutcome.Failed,
                _ => PaymentVerificationOutcome.Pending
            };

            return new PaymentVerification(outcome, amount, currency, id, txRef, $"Provider status: {status}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Flutterwave verification error");
            return new PaymentVerification(PaymentVerificationOutcome.Error, null, null, null, expectedReference, "The payment provider could not be reached.");
        }
    }

    private static string? ReadText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private string CreateSignedReference(Guid vetRequestId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var payload = $"{vetRequestId:N}.{timestamp}.{nonce}";
        return $"FH_{vetRequestId:N}_{timestamp}_{nonce}_{Sign(payload)}";
    }

    public bool IsValidSignedReference(string reference)
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
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()[..24];
    }

    public async Task<SubaccountResult> CreateOrUpdateSubaccountAsync(
        VetSubaccountRequest request,
        CancellationToken ct = default)
    {
        // Simulated provider: derive a stable fake subaccount id so the split flow is testable.
        if (!LiveMode)
            return new SubaccountResult(true, $"RS_SIM_{request.VetPartnerId:N}", null);

        if (string.IsNullOrWhiteSpace(_secretKey))
            return new SubaccountResult(false, null, "Payment provider is not configured.");

        try
        {
            var existingSubaccountId = request.ExistingSubaccountId;
            if (existingSubaccountId?.StartsWith("RS_SIM_", StringComparison.OrdinalIgnoreCase) == true)
                existingSubaccountId = null;

            var isUpdate = !string.IsNullOrWhiteSpace(existingSubaccountId);
            var url = isUpdate
                ? $"{_baseUrl}/subaccounts/{Uri.EscapeDataString(existingSubaccountId!)}"
                : $"{_baseUrl}/subaccounts";

            using var httpRequest = new HttpRequestMessage(isUpdate ? HttpMethod.Put : HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secretKey);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    account_bank = request.BankCode.Trim(),
                    account_number = request.AccountNumber.Trim(),
                    business_name = request.BusinessName.Trim(),
                    business_email = request.Email.Trim(),
                    business_contact = request.BusinessName.Trim(),
                    business_mobile = request.Contact.Trim(),
                    business_contact_mobile = request.Contact.Trim(),
                    country = request.Country.Trim().ToUpperInvariant(),
                    split_type = "percentage",
                    // Platform commission is 5% of the vet charge, i.e. ~4.76% of the total charge.
                    // Each payment overrides this with the exact rate.
                    split_value = 0.047619
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Flutterwave subaccount request failed with {StatusCode}", response.StatusCode);
                return new SubaccountResult(false, null, "The payout account could not be verified with the payment provider.");
            }

            if (isUpdate)
                return new SubaccountResult(true, existingSubaccountId, null);

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return new SubaccountResult(false, null, "The payment provider did not return a subaccount id.");

            var subaccountId = ReadText(data, "subaccount_id") ?? ReadText(data, "id");
            return string.IsNullOrWhiteSpace(subaccountId)
                ? new SubaccountResult(false, null, "The payment provider did not return a subaccount id.")
                : new SubaccountResult(true, subaccountId, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Flutterwave subaccount error");
            return new SubaccountResult(false, null, "The payment provider is unavailable.", true);
        }
    }

    public bool VerifyWebhookSignature(string rawBody, string? flutterwaveSignature, string? verifHash, string secretHash)
    {
        if (string.IsNullOrWhiteSpace(secretHash))
            return false;

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
