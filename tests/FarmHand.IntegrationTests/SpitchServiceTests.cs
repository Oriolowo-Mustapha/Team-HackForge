using System.Net;
using FarmHand.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FarmHand.IntegrationTests;

public class SpitchServiceTests
{
    private sealed class UnavailableProvider : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("PRIVATE_PROVIDER_DIAGNOSTIC") });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProviderErrors_DoNotIncludeRawProviderData(bool speech)
    {
        using var http = new HttpClient(new UnavailableProvider());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Spitch:ApiKey"] = "unit-test-key" }).Build();
        var service = new SpitchService(http, configuration, NullLogger<SpitchService>.Instance);
        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (speech) await service.SynthesizeSpeechAsync("Test instructions", "amina");
            else { using var audio = new MemoryStream(new byte[] { 0 }); await service.TranscribeAudioAsync(audio, "sample.wav", "audio/wav"); }
        });
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.DoesNotContain("PRIVATE_PROVIDER_DIAGNOSTIC", exception.Message);
    }
}
