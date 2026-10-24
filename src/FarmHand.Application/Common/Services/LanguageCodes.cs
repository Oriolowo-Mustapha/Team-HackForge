namespace FarmHand.Application.Common.Services;

/// <summary>
/// Canonical codes for the report languages FarmHand supports, matching the speech engines
/// (Spitch/YarnGPT): "yo" Yoruba, "ha" Hausa, "ig" Igbo, "pcm" Nigerian Pidgin, "en" English.
/// </summary>
public static class LanguageCodes
{
    public static readonly string[] Supported = ["yo", "ha", "ig", "pcm", "en"];

    /// <summary>Maps a detected language value (code or provider name) to a canonical code, or null when unsupported.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "yo" or "yor" or "yoruba" => "yo",
            "ha" or "hau" or "hausa" => "ha",
            "ig" or "ibo" or "igbo" => "ig",
            "pcm" or "pidgin" or "naija" => "pcm",
            "en" or "english" => "en",
            _ => null
        };
    }

    /// <summary>Human-readable display name for a language code, or null when unknown.</summary>
    public static string? DisplayName(string? code) => Normalize(code) switch
    {
        "yo" => "Yoruba",
        "ha" => "Hausa",
        "ig" => "Igbo",
        "pcm" => "Nigerian Pidgin",
        "en" => "English",
        _ => null
    };
}
