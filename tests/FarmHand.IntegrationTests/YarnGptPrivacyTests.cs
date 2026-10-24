using System.Net;
using FarmHand.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FarmHand.IntegrationTests;

public class YarnGptPrivacyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProviderFailures_DoNotLogOrExposeProviderBody(bool speech)
    {
        using var http = new HttpClient(new Failure());
        var logger = new CapturingLogger<YarnGptService>();
        var service = new YarnGptService(http, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["YarnGpt:ApiKey"] = "test-key" }).Build(), logger);
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (speech) await service.SynthesizeSpeechAsync("Test", "umar");
            else { using var stream = new MemoryStream([0]); await service.TranscribeAudioAsync(stream, "test.wav", "audio/wav", "test"); }
        });
        Assert.Equal(HttpStatusCode.BadGateway, error.StatusCode);
        Assert.DoesNotContain("PRIVATE_PROVIDER_DATA", error.ToString());
        Assert.DoesNotContain("PRIVATE_PROVIDER_DATA", string.Join(" ", logger.Messages));
    }

    private sealed class Failure : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("PRIVATE_PROVIDER_DATA") });
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception);
    }
}
