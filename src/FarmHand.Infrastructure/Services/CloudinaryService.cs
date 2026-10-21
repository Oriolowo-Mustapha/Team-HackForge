using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FarmHand.Infrastructure.Services;

public record CloudinaryUploadResult(string PublicId, string SecureUrl, string Url, int Width, int Height, string Format);

public interface ICloudinaryService
{
    Task<CloudinaryUploadResult> UploadImageAsync(Stream imageStream, string fileName, string folder = "farmhand", CancellationToken ct = default);
    Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default);
}

public class CloudinaryService : ICloudinaryService
{
    private readonly HttpClient _httpClient;
    private readonly string _cloudName;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(HttpClient httpClient, IConfiguration configuration, ILogger<CloudinaryService> logger)
    {
        _httpClient = httpClient;
        _cloudName = configuration["Cloudinary:CloudName"] ?? string.Empty;
        _apiKey = configuration["Cloudinary:ApiKey"] ?? string.Empty;
        _apiSecret = configuration["Cloudinary:ApiSecret"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<CloudinaryUploadResult> UploadImageAsync(Stream imageStream, string fileName, string folder = "farmhand", CancellationToken ct = default)
    {
        var timestamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        var folderParam = $"folder={folder}";
        var timestampParam = $"timestamp={timestamp}";

        // Cloudinary signature: HMAC-SHA1 of sorted parameters concatenated with api_secret
        var toSign = $"{folderParam}&{timestampParam}&{_apiSecret}";
        var signature = ComputeSignature(toSign);

        var uploadUrl = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/*");
        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(_apiKey), "api_key");
        content.Add(new StringContent(folder), "folder");
        content.Add(new StringContent(timestamp), "timestamp");
        content.Add(new StringContent(signature), "signature");
        content.Add(new StringContent("auto"), "quality");
        content.Add(new StringContent("auto"), "fetch_format");

        var response = await _httpClient.PostAsync(uploadUrl, content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cloudinary upload failed: {StatusCode} {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Cloudinary upload failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        return new CloudinaryUploadResult(
            root.GetProperty("public_id").GetString()!,
            root.GetProperty("secure_url").GetString()!,
            root.GetProperty("url").GetString()!,
            root.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
            root.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
            root.TryGetProperty("format", out var f) ? f.GetString() ?? "jpg" : "jpg"
        );
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken ct = default)
    {
        var timestamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        var toSign = $"public_id={publicId}&timestamp={timestamp}&{_apiSecret}";
        var signature = ComputeSignature(toSign);

        var deleteUrl = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/destroy";

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(publicId), "public_id");
        content.Add(new StringContent(_apiKey), "api_key");
        content.Add(new StringContent(timestamp), "timestamp");
        content.Add(new StringContent(signature), "signature");

        var response = await _httpClient.PostAsync(deleteUrl, content, ct);
        return response.IsSuccessStatusCode;
    }

    private string ComputeSignature(string dataToSign)
    {
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_apiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
