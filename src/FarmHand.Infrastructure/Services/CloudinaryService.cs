using FarmHand.Application.Common.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Services;

/// <summary>Signed Cloudinary uploads. Only <c>folder</c> and <c>timestamp</c> are sent and signed.</summary>
public class CloudinaryImageStorage : IImageStorage
{
    private readonly HttpClient _httpClient;
    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(HttpClient httpClient, IConfiguration configuration, ILogger<CloudinaryImageStorage> logger)
    {
        _httpClient = httpClient;
        _cloudName = configuration["Cloudinary:CloudName"] ?? string.Empty;
        _apiKey = configuration["Cloudinary:ApiKey"] ?? string.Empty;
        _apiSecret = configuration["Cloudinary:ApiSecret"] ?? string.Empty;
        _logger = logger;
    }

    public string Provider => "cloudinary";

    public static bool IsConfigured(IConfiguration configuration) =>
        AI.Clients.GeminiAiClient.IsRealKey(configuration["Cloudinary:CloudName"])
        && AI.Clients.GeminiAiClient.IsRealKey(configuration["Cloudinary:ApiKey"])
        && AI.Clients.GeminiAiClient.IsRealKey(configuration["Cloudinary:ApiSecret"]);

    public async Task<StoredImage> StoreAsync(byte[] content, string contentType, string folder, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign($"folder={folder}&timestamp={timestamp}{_apiSecret}");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "upload");
        form.Add(new StringContent(_apiKey), "api_key");
        form.Add(new StringContent(folder), "folder");
        form.Add(new StringContent(timestamp), "timestamp");
        form.Add(new StringContent(signature), "signature");

        try
        {
            using var response = await _httpClient.PostAsync($"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_cloudName)}/image/upload", form, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cloudinary upload failed with {StatusCode}", response.StatusCode);
                throw new ExternalServiceException("cloudinary", "The photo could not be stored right now. Please try again.");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var url = doc.RootElement.GetProperty("secure_url").GetString()!;
            var publicId = doc.RootElement.GetProperty("public_id").GetString()!;
            return new StoredImage(url, publicId, Provider);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Cloudinary upload error");
            throw new ExternalServiceException("cloudinary", "The photo could not be stored right now. Please try again.", ex);
        }
    }

    private string Sign(string data)
    {
        using var sha = SHA1.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}

/// <summary>
/// Development/demo image storage on local disk. Files get random 128-bit names and are served only to
/// authenticated users through <c>GET /api/media/{key}</c>. Container disks are ephemeral, so use
/// Cloudinary for durable production storage.
/// </summary>
public class LocalImageStorage : IImageStorage
{
    private readonly string _root;

    public LocalImageStorage(IConfiguration configuration)
    {
        _root = Path.GetFullPath(configuration["Storage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads"));
        Directory.CreateDirectory(_root);
    }

    public string Provider => "local";

    public string Root => _root;

    public async Task<StoredImage> StoreAsync(byte[] content, string contentType, string folder, CancellationToken ct = default)
    {
        var extension = contentType switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            "image/heic" => "heic",
            _ => "jpg"
        };
        var key = $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(_root, key), content, ct);
        return new StoredImage($"/api/media/{key}", key, Provider);
    }

    /// <summary>Returns the absolute path for a key, or null if the key is malformed or missing.</summary>
    public string? ResolvePath(string key)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-f0-9]{32}\\.(jpg|png|webp|heic)$"))
            return null;
        var path = Path.Combine(_root, key);
        return File.Exists(path) ? path : null;
    }
}
