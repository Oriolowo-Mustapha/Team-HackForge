using System;
using Microsoft.Extensions.Configuration;
using Xunit;
using FarmHand.Infrastructure.Services;

namespace FarmHand.Infrastructure.Tests;
{
    public class ConfigurationValidatorTests
    {
        private IConfiguration BuildConfig(Action<IConfigurationBuilder> configure)
        {
            var builder = new ConfigurationBuilder();
            configure(builder);
            return builder.Build();
        }

        [Fact]
        public void UnknownFlutterwaveMode_IsTreatedAsDisabled()
        {
            var config = BuildConfig(b => b.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("Flutterwave:Mode", "Test"), // unknown value
                new KeyValuePair<string, string>("App:PublicUrl", "https://example.com"),
                new KeyValuePair<string, string>("Jwt:SecretKey", new string('a', 32)),
                new KeyValuePair<string, string>("Security:FieldEncryptionKey", "key"),
                new KeyValuePair<string, string>("Brevo:Enabled", "true"),
                new KeyValuePair<string, string>("Brevo:ApiKey", "key"),
                new KeyValuePair<string, string>("Brevo:FromAddress", "test@example.com"),
                new KeyValuePair<string, string>("Storage:Provider", "Local"),
                new KeyValuePair<string, string>("Storage:LocalPath", "/tmp"),
                new KeyValuePair<string, string>("AllowedHosts", "localhost"),
            }));

            // Should not throw despite unknown mode value.
            var ex = Record.Exception(() => ConfigurationValidator.Validate(config, production: true));
            Assert.Null(ex);
        }

        [Fact]
        public void MissingConnectionString_DoesNotThrowWhenOptional()
        {
            var config = BuildConfig(b => b.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("App:PublicUrl", "https://example.com"),
                new KeyValuePair<string, string>("Jwt:SecretKey", new string('a', 32)),
                new KeyValuePair<string, string>("Security:FieldEncryptionKey", "key"),
                new KeyValuePair<string, string>("Brevo:Enabled", "true"),
                new KeyValuePair<string, string>("Brevo:ApiKey", "key"),
                new KeyValuePair<string, string>("Brevo:FromAddress", "test@example.com"),
                new KeyValuePair<string, string>("Storage:Provider", "Local"),
                new KeyValuePair<string, string>("Storage:LocalPath", "/tmp"),
                new KeyValuePair<string, string>("AllowedHosts", "localhost"),
            }));

            var ex = Record.Exception(() => ConfigurationValidator.Validate(config, production: true));
            Assert.Null(ex);
        }
    }
}
