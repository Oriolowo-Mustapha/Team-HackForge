using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("farmhand-timing-equalizer");

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;

    public AuthController(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
    }

    /// <summary>Register a new farmer account.</summary>
    /// <response code="201">Account created; use <c>POST /api/auth/login</c> to receive a token.</response>
    /// <response code="400">Validation failed or email already registered.</response>
    /// <response code="429">Too many attempts.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(email, ct))
            return BadRequest(new { error = "Email already registered." });

        var passwordError = ValidatePassword(request.Password);
        if (passwordError is not null)
            return BadRequest(new { error = passwordError });

        var user = new User(
            email,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            "Farmer",
            request.FullName?.Trim(),
            request.PhoneNumber?.Trim());

        await _unitOfWork.Users.AddAsync(user, ct);

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

    /// <summary>Login and receive a JWT token.</summary>
    /// <response code="200">Credentials accepted.</response>
    /// <response code="401">Invalid email or password, or the account is inactive.</response>
    /// <response code="429">Too many attempts.</response>
    [HttpPost("login")]
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
            return Unauthorized(new { error = "Invalid email or password." });

        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, new[] { user.Role }, user.TokenVersion);

        return Ok(new
        {
            message = "Login successful.",
            userId = user.Id,
            email = user.Email,
            role = user.Role,
            token = token.AccessToken,
            expiresAt = token.ExpiresAt
        });
    }

    /// <summary>Refresh an access token using a refresh token.</summary>
    /// <response code="200">New access token issued.</response>
    /// <response code="401">Refresh token invalid, expired, or revoked.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var existing = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (existing is null || !existing.IsActive)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var user = await _userRepository.GetByIdAsync(existing.UserId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "User account is inactive." });

        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email, new[] { user.Role }, user.TokenVersion);

        // Rotate the refresh token: revoke the old one and issue a new one.
        existing.Revoke();
        var newRefreshToken = new RefreshToken(existing.UserId, Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(7));
        await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "Token refreshed.",
            userId = user.Id,
            email = user.Email,
            role = user.Role,
            token = token.AccessToken,
            refreshToken = newRefreshToken.Token,
            expiresAt = token.ExpiresAt
        });
    }

    /// <summary>Revoke all refresh tokens for the current user (logout everywhere).</summary>
    /// <response code="200">All refresh tokens revoked.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "All sessions revoked." });
    }

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

    private static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return "Password must be at least 8 characters.";

        if (System.Text.Encoding.UTF8.GetByteCount(password) > 72)
            return "Password must be at most 72 bytes.";

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

public record RefreshRequest([Required] string RefreshToken);
