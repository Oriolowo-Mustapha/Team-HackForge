using FarmHand.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FarmHand.IntegrationTests;

public class NombaPaymentServiceTests
{
    private static NombaPaymentService CreateService(string signingSecret, string mode = "Simulated") =>
        new(
            new HttpClient(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Nomba:Mode"] = mode,
                    ["Nomba:SigningSecret"] = signingSecret
                })
                .Build(),
            NullLogger<NombaPaymentService>.Instance);

    [Fact]
    public async Task Initiate_SimulatedMode_ReturnsSignedReference()
    {
        var service = CreateService("integration-test-signing-secret");
        var vetRequestId = Guid.NewGuid();

        var result = await service.InitiatePaymentAsync(vetRequestId, 1500, "NGN");

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
        var initiated = await service.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN");

        var verified = await service.VerifyPaymentAsync(initiated.TransactionReference!);

        Assert.True(verified.Success);
    }

    [Fact]
    public async Task Verify_ReferenceSignedWithDifferentSecret_Fails()
    {
        var issuer = CreateService("integration-test-signing-secret");
        var verifier = CreateService("a-completely-different-secret");
        var initiated = await issuer.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN");

        var verified = await verifier.VerifyPaymentAsync(initiated.TransactionReference!);

        Assert.False(verified.Success);
    }

    [Fact]
    public async Task Verify_TamperedReference_Fails()
    {
        var service = CreateService("integration-test-signing-secret");
        var initiated = await service.InitiatePaymentAsync(Guid.NewGuid(), 1500, "NGN");
        var reference = initiated.TransactionReference!;
        var tampered = reference[..^1] + (reference[^1] == 'a' ? 'b' : 'a');

        var verified = await service.VerifyPaymentAsync(tampered);

        Assert.False(verified.Success);
    }

    [Fact]
    public async Task Initiate_NonPositiveAmount_Fails()
    {
        var service = CreateService("integration-test-signing-secret");

        var result = await service.InitiatePaymentAsync(Guid.NewGuid(), 0, "NGN");

        Assert.False(result.Success);
    }
}
