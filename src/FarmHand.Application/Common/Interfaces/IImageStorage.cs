namespace FarmHand.Application.Common.Interfaces;

public record StoredImage(string Url, string StorageKey, string Provider);

/// <summary>Stores user-uploaded images (Cloudinary in production, local disk for development and demos).</summary>
public interface IImageStorage
{
    string Provider { get; }

    Task<StoredImage> StoreAsync(byte[] content, string contentType, string folder, CancellationToken ct = default);
}
