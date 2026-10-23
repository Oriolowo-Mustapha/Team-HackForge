using System.Text;

namespace FarmHand.Domain.Services;

public static class RefreshTokenHasher
{
    public static string HashToken(string token)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
