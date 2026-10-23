namespace FarmHand.Application.Common.Interfaces;

public record TokenResult(string AccessToken, DateTime ExpiresAt);

public interface IJwtService
{
    TokenResult GenerateToken(string userId, string email, IEnumerable<string> roles, int tokenVersion = 0, bool mustChangePassword = false);
    bool ValidateToken(string token, out string? userId, out int? tokenVersion);
}
