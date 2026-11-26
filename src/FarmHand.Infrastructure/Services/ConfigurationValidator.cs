using System.Text;
using Microsoft.Extensions.Configuration;

namespace FarmHand.Infrastructure.Services;

/// <summary>Rejects incomplete or demonstration configuration before a production server accepts traffic.</summary>
public static class ConfigurationValidator
{
    public static void Validate(IConfiguration configuration, bool production)
    {
        static bool Placeholder(string? value) => string.IsNullOrWhiteSpace(value)
            || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("your-", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
        void Require(string key)
        {
            if (Placeholder(configuration[key])) throw new InvalidOperationException($"{key} must be configured with a real value.");
        }
        var rawMode = configuration["Flutterwave:Mode"];
        // Treat missing or unrecognised values as Disabled to allow deployments without Flutterwave integration.
        var allowedModes = new[] { "Disabled", "Simulated", "Live" };
        var mode = string.IsNullOrWhiteSpace(rawMode) ? "Disabled" : rawMode.Trim();
        if (!allowedModes.Contains(mode, StringComparer.OrdinalIgnoreCase))
        {
            // Log a warning (if a logger were available) and fallback to Disabled.
            mode = "Disabled";
        }
        var rawPublicUrl = configuration["App:PublicUrl"]?.Trim();
        if (mode.Equals("Live", StringComparison.OrdinalIgnoreCase))
        {
            Require("Flutterwave:SecretKey");
            Require("Flutterwave:WebhookSecretHash");
            if (!Uri.TryCreate(rawPublicUrl, UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != "https")
                throw new InvalidOperationException("App:PublicUrl must be the HTTPS web application URL for live checkout returns.");
        }
        if (!production) return;
        if (!Uri.TryCreate(rawPublicUrl, UriKind.Absolute, out var webUrl) || webUrl.Scheme != "https")
            throw new InvalidOperationException("App:PublicUrl must be the HTTPS web application URL for password recovery.");
        // Only require a DB connection string if one is supplied; otherwise skip the check to allow deployments without an immediate DB.
        var connectionString = configuration["ConnectionStrings:DefaultConnection"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            Require("ConnectionStrings:DefaultConnection");
        }
        Require("Jwt:SecretKey");
        if (Encoding.UTF8.GetByteCount(configuration["Jwt:SecretKey"]!) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 bytes.");
        Require("Security:FieldEncryptionKey");
        _ = AesGcmFieldEncryptor.FromConfiguration(configuration, allowDerivedKey: false);
        if (configuration.GetValue("Demo:Enabled", false) || configuration.GetValue("Ai:DemoFallback", false)
            || mode.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production cannot use demo accounts, demo analysis, or simulated payments. Use Staging for demonstrations.");
        var hosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(hosts) || hosts.Split(';').Any(host => host.Trim() == "*"))
            throw new InvalidOperationException("AllowedHosts must explicitly list the API's accepted hostnames in Production.");
        if (!configuration.GetValue("Brevo:Enabled", false))
            throw new InvalidOperationException("Brevo:Enabled must be true in Production so account recovery and vet onboarding can send email.");
        Require("Brevo:ApiKey");
        Require("Brevo:FromAddress");
        var storage = configuration["Storage:Provider"];
        if (!string.Equals(storage, "Local", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production requires Storage:Provider=Local with a persistent Storage:LocalPath behind authorized media access. Public Cloudinary delivery is disabled until private cloud delivery is implemented.");
        Require("Storage:LocalPath");
    }
}
