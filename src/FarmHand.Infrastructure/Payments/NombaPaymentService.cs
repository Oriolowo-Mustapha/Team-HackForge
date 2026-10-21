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
        // Sandbox mode: simulate payment verification
        _logger.LogInformation("Nomba sandbox: verifying payment {TransactionReference}", transactionReference);
        await Task.Delay(100, ct);

        return new PaymentResult(true, transactionReference, null);
    }
}
