using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FarmHand.Infrastructure.Services;

public class JwtService : IJwtService
{
    /// <summary>Present on tokens issued to accounts that must replace a temporary password.</summary>
    public const string PasswordChangeClaim = "pwd_change_required";

    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtService(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        _secretKey = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is required.");
        _issuer = jwtSection["Issuer"] ?? "FarmHand";
        _audience = jwtSection["Audience"] ?? "FarmHandUsers";
        _expirationMinutes = int.TryParse(jwtSection["ExpirationMinutes"], out var minutes) && minutes > 0
            ? minutes
            : 60;
    }

    public TokenResult GenerateToken(string userId, string email, IEnumerable<string> roles, int tokenVersion = 0, bool mustChangePassword = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new("token_version", tokenVersion.ToString())
        };

        if (mustChangePassword)
            claims.Add(new Claim(PasswordChangeClaim, "true"));

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResult(tokenString, token.ValidTo);
    }

    public bool ValidateToken(string token, out string? userId, out int? tokenVersion)
    {
        userId = null;
        tokenVersion = null;
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out _);
            userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var versionClaim = principal.FindFirst("token_version")?.Value;
            if (int.TryParse(versionClaim, out var version))
                tokenVersion = version;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
