using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FarmHand.Infrastructure.Services;

/// <summary>
/// AES-256-GCM field encryption. Ciphertext format: "v1:" + base64(nonce[12] | ciphertext | tag[16]).
/// The key comes from <c>Security:FieldEncryptionKey</c> (base64, 32 bytes).
/// </summary>
public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const string Prefix = "v1:";
    private readonly byte[] _key;

    public AesGcmFieldEncryptor(byte[] key)
    {
        if (key.Length != 32)
            throw new InvalidOperationException("The field encryption key must be 32 bytes.");
        _key = key;
    }

    /// <summary>
    /// Reads the key from configuration. When it is missing and <paramref name="allowDerivedKey"/> is true
    /// (development and demo environments only), a key is derived from the JWT secret with HKDF.
    /// </summary>
    public static AesGcmFieldEncryptor FromConfiguration(IConfiguration configuration, bool allowDerivedKey)
    {
        var configured = configuration["Security:FieldEncryptionKey"];
        if (!string.IsNullOrWhiteSpace(configured) && !configured.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            byte[] key;
            try { key = Convert.FromBase64String(configured); }
            catch (FormatException) { throw new InvalidOperationException("Security:FieldEncryptionKey must be base64."); }
            return new AesGcmFieldEncryptor(key);
        }

        if (!allowDerivedKey)
            throw new InvalidOperationException(
                "Security:FieldEncryptionKey is required outside Development. Generate one with: openssl rand -base64 32");

        var jwtSecret = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is required.");
        var derived = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSecret), 32,
            salt: Encoding.UTF8.GetBytes("farmhand-field-encryption"), info: Encoding.UTF8.GetBytes("v1"));
        return new AesGcmFieldEncryptor(derived);
    }

    public string Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var payload = new byte[nonce.Length + cipher.Length + tag.Length];
        nonce.CopyTo(payload, 0);
        cipher.CopyTo(payload, nonce.Length);
        tag.CopyTo(payload, nonce.Length + cipher.Length);
        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        if (!ciphertext.StartsWith(Prefix, StringComparison.Ordinal))
            return ciphertext; // legacy plaintext value written before encryption was introduced

        var payload = Convert.FromBase64String(ciphertext[Prefix.Length..]);
        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(payload.Length - 16, 16);
        var cipher = payload.AsSpan(12, payload.Length - 28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
