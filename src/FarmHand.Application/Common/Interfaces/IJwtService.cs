namespace FarmHand.Application.Common.Interfaces;

public record TokenResult(string AccessToken, DateTime ExpiresAt);

public interface IJwtService
{
    TokenResult GenerateToken(string userId, string email, IEnumerable<string> roles);
    bool ValidateToken(string token, out string? userId);
}
