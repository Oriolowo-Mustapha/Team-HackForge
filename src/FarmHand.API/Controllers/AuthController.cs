using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("farmhand-timing-equalizer");
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IAuditLogger _audit;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IJwtService jwtService,
        IAuditLogger audit,
        IEmailService email,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _audit = audit;
        _email = email;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Register a new farmer account. Veterinarian and administrator accounts are created by administrators only.</summary>
    /// <response code="201">Account created; use <c>POST /api/auth/login</c> to receive a token.</response>
    /// <response code="400">Validation failed or email already registered.</response>
    /// <response code="429">Too many attempts.</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var passwordError = ValidatePassword(request.Password);
        if (passwordError is not null)
            return BadRequest(new { error = passwordError });

        if (await _userRepository.ExistsByEmailAsync(email, ct))
            return BadRequest(new { error = "Email already registered." });

        var user = new User(
            email,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            UserRoles.Farmer,
            request.FullName?.Trim(),
            request.PhoneNumber?.Trim());

        await _unitOfWork.Users.AddAsync(user, ct);
        await _audit.RecordAsync("auth.registered", user.Id, actorIdOverride: user.Id.ToString(), ct: ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { error = "Email already registered." });
        }

        return CreatedAtAction(nameof(Login), new { }, new
        {
            message = "Account created successfully. Please log in to receive your access token."
        });
    }

    /// <summary>Login and receive a JWT access token plus a rotating refresh token.</summary>
    /// <remarks>
    /// When <c>mustChangePassword</c> is true (administrator-issued temporary password) the token only
    /// allows <c>GET /api/auth/me</c>, <c>POST /api/auth/change-password</c> and logout until the password is changed.
    /// </remarks>
    /// <response code="200">Credentials accepted.</response>
    /// <response code="401">Invalid email or password, or the account is inactive.</response>
    /// <response code="429">Too many attempts.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, ct);

        // Always run a BCrypt verification so unknown emails take the same time as wrong passwords.
        var passwordValid = PasswordMatches(request.Password, user?.PasswordHash);

        if (user is null || !user.IsActive || !passwordValid)
        {
            _logger.LogInformation("Failed sign-in attempt");
            return Unauthorized(new { error = "Invalid email or password." });
        }

        user.RecordLogin();
        await _audit.RecordAsync("auth.login", user.Id, actorIdOverride: user.Id.ToString(), ct: ct);
        var session = await IssueSessionAsync(user, ct);
        return Ok(session);
    }

    /// <summary>Exchange a refresh token for a new access token. Refresh tokens rotate on every use.</summary>
    /// <remarks>Presenting a refresh token that was already used revokes every session for that account (token theft protection).</remarks>
    /// <response code="200">New tokens issued.</response>
    /// <response code="401">Refresh token invalid, expired, revoked, or already used.</response>
    [HttpPost("refresh")]
    [EnableRateLimiting("sessions")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var existing = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (existing is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        // Concurrent refreshes from the same client (e.g. two tabs) can race; a reuse within a short
        // grace window is treated as that race, not as theft.
        if (existing.RevokedAt is { } revokedAt && DateTime.UtcNow - revokedAt < TimeSpan.FromSeconds(30))
            return Unauthorized(new { error = "This refresh token was already used.", code = "refresh_raced" });

        if (existing.RevokedAt is not null)
        {
            // A rotated token was replayed: assume theft and end every session for the account.
            await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(existing.UserId, ct);
            if (await _userRepository.GetByIdAsync(existing.UserId, ct) is { } victim)
                victim.IncrementTokenVersion();
            await _audit.RecordAsync("auth.refresh_reuse_detected", existing.UserId, actorIdOverride: existing.UserId.ToString(), ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Unauthorized(new { error = "This session has ended. Please sign in again." });
        }

        if (!existing.IsActive)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var user = await _userRepository.GetByIdAsync(existing.UserId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "User account is inactive." });

        // Atomic rotation: two concurrent refreshes with the same token cannot both succeed.
        if (!await _unitOfWork.RefreshTokens.TryRevokeAsync(existing.Id, ct))
            return Unauthorized(new { error = "This refresh token was already used." });

        var session = await IssueSessionAsync(user, ct);
        return Ok(session);
    }

    /// <summary>Sign out of this device by revoking the supplied refresh token. Idempotent.</summary>
    /// <response code="204">Signed out.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var existing = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (existing is not null && existing.RevokedAt is null)
            await _unitOfWork.RefreshTokens.TryRevokeAsync(existing.Id, ct);
        return NoContent();
    }

    /// <summary>Sign out everywhere: revokes all refresh tokens and invalidates all access tokens for the current user.</summary>
    /// <response code="200">All sessions revoked.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();

        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId, ct);
        if (await _userRepository.GetByIdAsync(userId, ct) is { } user)
            user.IncrementTokenVersion();
        await _audit.RecordAsync("auth.sessions_revoked", userId, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "All sessions revoked." });
    }

    /// <summary>Current user's profile, farms and vet profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized();
        return Ok(await BuildProfileAsync(user, ct));
    }

    /// <summary>Update the current user's name and phone number.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized();

        user.UpdateProfile(Clean(request.FullName), Clean(request.PhoneNumber));
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(await BuildProfileAsync(user, ct));
    }

    /// <summary>Change the current user's password and invalidate existing sessions.</summary>
    /// <response code="200">Password changed; sign in again with the new password.</response>
    /// <response code="400">Current password is incorrect or the new password is invalid.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (CurrentUserId() is not { } userId)
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "User account is inactive." });

        if (!PasswordMatches(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect." });

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            return BadRequest(new { error = "New password must be different from the current password." });

        var passwordError = ValidatePassword(request.NewPassword);
        if (passwordError is not null)
            return BadRequest(new { error = passwordError });

        user.ChangePassword(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        user.IncrementTokenVersion();
        _unitOfWork.Users.Update(user);
        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id, ct);
        await _audit.RecordAsync("auth.password_changed", user.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "Password changed. Please sign in again." });
    }

    /// <summary>Request a password reset link. Always returns 202 so account existence is not revealed.</summary>
    /// <remarks>
    /// The link is emailed when email delivery is configured. In the Development environment without email,
    /// the link is written to the server log instead (never returned in the response).
    /// </remarks>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user is { IsActive: true })
        {
            await _unitOfWork.PasswordResetTokens.InvalidateAllForUserAsync(user.Id, ct);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            await _unitOfWork.PasswordResetTokens.AddAsync(
                new PasswordResetToken(user.Id, RefreshTokenHasher.HashToken(token), DateTime.UtcNow.Add(ResetLifetime)), ct);
            await _audit.RecordAsync("auth.password_reset_requested", user.Id, actorIdOverride: user.Id.ToString(), ct: ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var publicUrl = (_configuration["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');
            var link = $"{publicUrl}/reset-password?token={token}";

            var sent = await _email.SendAsync(new EmailMessage(
                user.Email,
                "Reset your FarmHand password",
                $"""
                <div style="font-family:Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2937">
                  <h2 style="color:#3f5221">Reset your password</h2>
                  <p>Someone asked to reset the password for your FarmHand account. If it was you, use the link below within 30 minutes.</p>
                  <p><a href="{WebUtility.HtmlEncode(link)}" style="background:#0b0b06;color:#fff;padding:12px 20px;border-radius:12px;text-decoration:none">Choose a new password</a></p>
                  <p style="color:#6b7280;font-size:12px">If you did not ask for this, you can ignore this email. Your password will not change.</p>
                </div>
                """,
                $"Reset your FarmHand password within 30 minutes: {link}\n\nIf you did not ask for this, ignore this email."), ct);

            if (!sent && _environment.IsDevelopment())
                _logger.LogWarning("Email delivery is disabled. Development password reset link for {Email}: {Link}", user.Email, link);
        }

        return Accepted(new { message = "If an account exists for that email, a reset link is on its way." });
    }

    /// <summary>Set a new password using a reset token. Ends every existing session.</summary>
    /// <response code="200">Password reset.</response>
    /// <response code="400">The token is invalid or expired, or the password is invalid.</response>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var passwordError = ValidatePassword(request.NewPassword);
        if (passwordError is not null)
            return BadRequest(new { error = passwordError });

        var token = await _unitOfWork.PasswordResetTokens.GetByHashAsync(RefreshTokenHasher.HashToken(request.Token.Trim()), ct);
        if (token is null || !token.IsUsable)
            return BadRequest(new { error = "This reset link is invalid or has expired. Request a new one." });

        var user = await _userRepository.GetByIdAsync(token.UserId, ct);
        if (user is null || !user.IsActive)
            return BadRequest(new { error = "This reset link is invalid or has expired. Request a new one." });

        token.MarkUsed();
        user.ChangePassword(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        user.IncrementTokenVersion();
        _unitOfWork.Users.Update(user);
        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(user.Id, ct);
        await _audit.RecordAsync("auth.password_reset", user.Id, actorIdOverride: user.Id.ToString(), ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "Your password has been reset. Sign in with your new password." });
    }

    private async Task<object> IssueSessionAsync(User user, CancellationToken ct)
    {
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, new[] { user.Role }, user.TokenVersion, user.MustChangePassword);
        var refreshTokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken(
            user.Id,
            RefreshTokenHasher.HashToken(refreshTokenValue),
            DateTime.UtcNow.Add(RefreshLifetime)), ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new
        {
            message = "Login successful.",
            userId = user.Id,
            email = user.Email,
            role = user.Role,
            fullName = user.FullName,
            mustChangePassword = user.MustChangePassword,
            token = token.AccessToken,
            refreshToken = refreshTokenValue,
            expiresAt = token.ExpiresAt,
            refreshExpiresAt = DateTime.UtcNow.Add(RefreshLifetime)
        };
    }

    private async Task<object> BuildProfileAsync(User user, CancellationToken ct)
    {
        var farms = await _unitOfWork.Farms.ListByOwnerIdAsync(user.Id.ToString(), ct);
        var vet = user.Role == UserRoles.Vet ? await _unitOfWork.VetPartners.GetByUserIdAsync(user.Id, ct) : null;
        return new
        {
            userId = user.Id,
            email = user.Email,
            role = user.Role,
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            mustChangePassword = user.MustChangePassword,
            createdAt = user.CreatedAt,
            lastLoginAt = user.LastLoginAt,
            farms = farms.Select(f => new { id = f.Id, displayName = f.DisplayName, location = f.Location }),
            vetProfile = vet is null ? null : new
            {
                id = vet.Id,
                name = vet.Name,
                specialization = vet.Specialization,
                contact = vet.Contact,
                isActive = vet.IsActive,
                hasPayoutAccount = vet.HasPayoutAccount
            }
        };
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool PasswordMatches(string? password, string? passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password ?? string.Empty, passwordHash ?? DummyPasswordHash);
        }
        catch (Exception)
        {
            // Treat malformed legacy hashes as a failed login.
            return false;
        }
    }

    internal static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return "Password must be at least 8 characters.";

        if (System.Text.Encoding.UTF8.GetByteCount(password) > 72)
            return "Password must be at most 72 bytes.";

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must contain at least one letter and one number.";

        return null;
    }
}

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required] string Password,
    [MaxLength(200)] string? FullName,
    [MaxLength(20)] string? PhoneNumber);

public record LoginRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required] string Password);

public record RefreshRequest([Required, MaxLength(200)] string RefreshToken);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(100)] string NewPassword);

public record UpdateProfileRequest([MaxLength(200)] string? FullName, [MaxLength(20)] string? PhoneNumber);

public record ForgotPasswordRequest([Required, EmailAddress, MaxLength(255)] string Email);

public record ResetPasswordRequest([Required, MaxLength(200)] string Token, [Required, MaxLength(100)] string NewPassword);
