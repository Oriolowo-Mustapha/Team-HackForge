using FarmHand.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FarmHand.IntegrationTests;

public class FlutterwavePaymentServiceTests
{
    private static FlutterwavePaymentService CreateService(string signingSecret, string mode = "Simulated") =>
        new(
            new HttpClient(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Flutterwave:Mode"] = mode,
                    ["Flutterwave:SigningSecret"] = signingSecret
                })
                .Build(),
            NullLogger<FlutterwavePaymentService>.Instance);

    [Fact]
    public async Task Initiate_SimulatedMode_ReturnsSignedReference()
    {
        var service = CreateService("integration-test-signing-secret");
        var vetRequestId = Guid.NewGuid();

        var result = await service.InitiatePaymentAsync(vetRequestId, 1500, "NGN", "farmer@example.com");

        Assert.True(result.Success);
        Assert.NotNull(result.TransactionReference);
        Assert.StartsWith($"FH_{vetRequestId:N}_", result.TransactionReference);
        Assert.Equal(1500, result.Amount);
        Assert.Equal("NGN", result.Currency);
    }

    [Fact]
    public async Task Verify_ReferenceFromSameSecret_Succeeds()
    {
        var service = CreateService("integration-test-signing-secret");
        var initiated = await service.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN", "farmer@example.com");

        var verified = await service.VerifyPaymentAsync(initiated.TransactionReference!);

        Assert.True(verified.Success);
    }

    [Fact]
    public async Task Verify_ReferenceSignedWithDifferentSecret_Fails()
    {
        var issuer = CreateService("integration-test-signing-secret");
        var verifier = CreateService("a-completely-different-secret");
        var initiated = await issuer.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN", "farmer@example.com");

        var verified = await verifier.VerifyPaymentAsync(initiated.TransactionReference!);

        Assert.False(verified.Success);
    }

    [Fact]
    public async Task Verify_TamperedReference_Fails()
    {
        var service = CreateService("integration-test-signing-secret");
        var initiated = await service.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN", "farmer@example.com");
        var reference = initiated.TransactionReference!;
        var tampered = reference[..^1] + (reference[^1] == 'a' ? 'b' : 'a');

        var verified = await service.VerifyPaymentAsync(tampered);

        Assert.False(verified.Success);
    }

    [Fact]
    public async Task Initiate_NonPositiveAmount_Fails()
    {
        var service = CreateService("integration-test-signing-secret");

        var result = await service.InitiatePaymentAsync(Guid.NewGuid(), 0, "NGN", "farmer@example.com");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Initiate_MissingCustomerEmail_Fails()
    {
        var service = CreateService("integration-test-signing-secret");

        var result = await service.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN", "");

        Assert.False(result.Success);
    }

    [Fact]
    public void VerifyWebhookSignature_HmacSignature_ReturnsTrue()
    {
        var service = CreateService("webhook-secret-hash");
        var rawBody = "{\"event\":\"charge.completed\",\"data\":{\"id\":12345,\"tx_ref\":\"FH_ref\",\"status\":\"successful\"}}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("webhook-secret-hash"));
        var expected = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawBody)));

        var valid = service.VerifyWebhookSignature(rawBody, expected, null, "webhook-secret-hash");

        Assert.True(valid);
    }

    [Fact]
    public void VerifyWebhookSignature_TamperedBody_ReturnsFalse()
    {
        var service = CreateService("webhook-secret-hash");
        var rawBody = "{\"event\":\"charge.completed\",\"data\":{\"id\":12345,\"tx_ref\":\"FH_ref\",\"status\":\"successful\"}}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("webhook-secret-hash"));
        var expected = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawBody)));

        var valid = service.VerifyWebhookSignature(rawBody + "extra", expected, null, "webhook-secret-hash");

        Assert.False(valid);
    }

    [Fact]
    public void VerifyWebhookSignature_VerifHash_ReturnsTrue()
    {
        var service = CreateService("webhook-secret-hash");

        var valid = service.VerifyWebhookSignature("any body", null, "webhook-secret-hash", "webhook-secret-hash");

        Assert.True(valid);
    }

    [Fact]
    public void VerifyWebhookSignature_NoSignature_ReturnsFalse()
    {
        var service = CreateService("webhook-secret-hash");

        var valid = service.VerifyWebhookSignature("any body", null, null, "webhook-secret-hash");

        Assert.False(valid);
    }
}
