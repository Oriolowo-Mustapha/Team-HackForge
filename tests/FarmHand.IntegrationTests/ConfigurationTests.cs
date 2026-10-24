using FarmHand.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace FarmHand.IntegrationTests;

public class ConfigurationTests
{
    private static Dictionary<string, string?> Production() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=database;Database=farmhand;Username=app;Password=test-only",
        ["Jwt:SecretKey"] = new string('x', 48),
        ["Security:FieldEncryptionKey"] = Convert.ToBase64String(new byte[32]),
        ["AllowedHosts"] = "api;farmhand.example",
        ["App:PublicUrl"] = "https://farmhand.example",
        ["Flutterwave:Mode"] = "Disabled",
        ["Brevo:Enabled"] = "true",
        ["Brevo:ApiKey"] = "test-only-key",
        ["Brevo:FromAddress"] = "noreply@farmhand.example",
        ["Storage:Provider"] = "Local",
        ["Storage:LocalPath"] = "/data/uploads"
    };

    [Fact]
    public void CompleteProductionConfiguration_PassesValidation() =>
        ConfigurationValidator.Validate(new ConfigurationBuilder().AddInMemoryCollection(Production()).Build(), true);

    [Theory]
    [InlineData("Demo:Enabled", "true")]
    [InlineData("Ai:DemoFallback", "true")]
    [InlineData("Flutterwave:Mode", "Simulated")]
    [InlineData("Flutterwave:Mode", "Typo")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("App:PublicUrl", "http://localhost:3000")]
    [InlineData("Security:FieldEncryptionKey", "not-base64")]
    [InlineData("Brevo:Enabled", "false")]
    [InlineData("Jwt:SecretKey", "short")]
    [InlineData("Storage:LocalPath", "")]
    public void UnsafeProductionConfiguration_IsRejected(string key, string value)
    {
        var values = Production();
        values[key] = value;
        Assert.Throws<InvalidOperationException>(() => ConfigurationValidator.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true));
    }
}
