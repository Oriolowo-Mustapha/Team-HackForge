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
        var mode = configuration["Flutterwave:Mode"] ?? "Disabled";
        if (!new[] { "Disabled", "Simulated", "Live" }.Contains(mode, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Flutterwave:Mode must be Disabled, Simulated, or Live.");
        if (mode.Equals("Live", StringComparison.OrdinalIgnoreCase))
        {
            Require("Flutterwave:SecretKey");
            Require("Flutterwave:WebhookSecretHash");
            if (!Uri.TryCreate(configuration["App:PublicUrl"], UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != "https")
                throw new InvalidOperationException("App:PublicUrl must be the HTTPS web application URL for live checkout returns.");
        }
        if (!production) return;
        if (!Uri.TryCreate(configuration["App:PublicUrl"], UriKind.Absolute, out var webUrl) || webUrl.Scheme != "https")
            throw new InvalidOperationException("App:PublicUrl must be the HTTPS web application URL for password recovery.");
        Require("ConnectionStrings:DefaultConnection");
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
