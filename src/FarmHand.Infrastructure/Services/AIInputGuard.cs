using System.Text;
using System.Text.RegularExpressions;

namespace FarmHand.Infrastructure.Services;

public static class AIInputGuard
{
    private static readonly Regex ControlCharacters = new(@"[\u0000-\u001F\u007F]", RegexOptions.Compiled);

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = ControlCharacters.Replace(value.Trim(), " ");
        return clean.Length > 2000 ? clean[..2000] : clean;
    }

    public static string? SanitizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var clean = Sanitize(value);
        return clean.Length == 0 ? null : clean;
    }

    public static string SanitizeFacts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        return clean.Length > 3000 ? clean[..3000] : clean;
    }
}
