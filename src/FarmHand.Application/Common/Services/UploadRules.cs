using FarmHand.Application.Common.Exceptions;

namespace FarmHand.Application.Common.Services;

/// <summary>
/// Server-side validation for user uploads. The declared content type is not trusted: the file
/// signature (magic bytes) decides the type, and sizes are capped.
/// </summary>
public static class UploadRules
{
    public const long MaxImageBytes = 8 * 1024 * 1024;
    public const long MaxAudioBytes = 10 * 1024 * 1024;

    public static string ValidateImage(byte[] content)
    {
        if (content.Length == 0)
            throw PublicException.BadRequest("The image file is empty.");
        if (content.Length > MaxImageBytes)
            throw PublicException.BadRequest("Images must be 8 MB or smaller.");

        return DetectImageType(content)
            ?? throw PublicException.BadRequest("Unsupported image type. Use a JPEG, PNG, WebP or HEIC photo.");
    }

    public static string ValidateAudio(byte[] content)
    {
        if (content.Length == 0)
            throw PublicException.BadRequest("The audio recording is empty.");
        if (content.Length > MaxAudioBytes)
            throw PublicException.BadRequest("Voice recordings must be 10 MB or smaller (about 5 minutes).");

        return DetectAudioType(content)
            ?? throw PublicException.BadRequest("Unsupported audio type. Use WebM, Ogg, MP4/M4A, MP3 or WAV.");
    }

    public static string? DetectImageType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 12 && Ascii(b, 0, "RIFF") && Ascii(b, 8, "WEBP")) return "image/webp";
        if (b.Length >= 12 && Ascii(b, 4, "ftyp"))
        {
            var brand = System.Text.Encoding.ASCII.GetString(b.Slice(8, 4));
            if (brand is "heic" or "heix" or "hevc" or "heim" or "heis" or "mif1" or "msf1") return "image/heic";
        }
        return null;
    }

    public static string? DetectAudioType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 4 && b[0] == 0x1A && b[1] == 0x45 && b[2] == 0xDF && b[3] == 0xA3) return "audio/webm";
        if (b.Length >= 4 && Ascii(b, 0, "OggS")) return "audio/ogg";
        if (b.Length >= 12 && Ascii(b, 0, "RIFF") && Ascii(b, 8, "WAVE")) return "audio/wav";
        if (b.Length >= 3 && Ascii(b, 0, "ID3")) return "audio/mpeg";
        if (b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0) return "audio/mpeg";
        if (b.Length >= 12 && Ascii(b, 4, "ftyp")) return "audio/mp4";
        return null;
    }

    private static bool Ascii(ReadOnlySpan<byte> b, int offset, string text)
    {
        if (b.Length < offset + text.Length) return false;
        for (var i = 0; i < text.Length; i++)
            if (b[offset + i] != (byte)text[i]) return false;
        return true;
    }
}
