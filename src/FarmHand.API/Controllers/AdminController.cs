using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using FarmHand.Application.Features.Admin;
using FarmHand.Application.Features.Incidents;
using FarmHand.Application.Features.Payments;
using FarmHand.Infrastructure.Persistence;
using MediatR;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminController> _logger;
    private readonly IAuditLogger _audit;
    private readonly IMediator _mediator;

    public AdminController(
        IUserRepository userRepository,
        IVetPartnerRepository vetPartnerRepository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<AdminController> logger,
        IAuditLogger audit,
        IMediator mediator)
    {
        _audit = audit;
        _mediator = mediator;
        _userRepository = userRepository;
        _vetPartnerRepository = vetPartnerRepository;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Register a vet doctor on the system. Admin only.
    /// Creates the User account and VetPartner profile, then emails the login credentials.
    /// The temporary password is never returned in the API response.
    /// </summary>
    /// <response code="201">Vet registered; <c>credentialsEmailed</c> reports delivery.</response>
    /// <response code="400">Validation failed or email already registered.</response>
    [HttpPost("vets")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterVet([FromBody] RegisterVetRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(email, ct))
            return BadRequest(new { error = "Email already registered." });

        var temporaryPassword = request.TemporaryPassword;
        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            if (!_emailService.IsEnabled)
                return BadRequest(new { error = "Email delivery is disabled. Provide a temporary password." });

            temporaryPassword = GenerateTemporaryPassword();
        }
        else
        {
            if (AuthController.ValidatePassword(temporaryPassword) is { } passwordError)
                return BadRequest(new { error = passwordError });
        }

        var fullName = request.FullName?.Trim();
        var vetUser = new User(
            email,
            BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            UserRoles.Vet,
            fullName,
            request.PhoneNumber?.Trim());
        // Administrator-issued passwords must be replaced at first sign-in.
        vetUser.SetTemporaryPassword(vetUser.PasswordHash);

        var vetPartner = new VetPartner(
            vetUser.Id,
            fullName ?? email,
            email,
            request.Specialization,
            request.Contact);

        await _unitOfWork.Users.AddAsync(vetUser, ct);
        await _unitOfWork.VetPartners.AddAsync(vetPartner, ct);
        await _audit.RecordAsync("admin.vet_registered", vetPartner.Id, email, ct: ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { error = "Email already registered." });
        }

        var emailed = await _emailService.SendAsync(
            BuildCredentialsEmail(vetUser.Email, vetPartner.Name, temporaryPassword),
            ct);

        if (!emailed)
            _logger.LogWarning("Vet {VetId} registered but credentials email was not delivered", vetPartner.Id);

        return CreatedAtAction(nameof(ListVets), new { }, new
        {
            vetId = vetPartner.Id,
            userId = vetUser.Id,
            email = vetUser.Email,
            role = vetUser.Role,
            specialization = vetPartner.Specialization,
            credentialsEmailed = emailed,
            message = emailed
                ? "Vet doctor registered. Login credentials were emailed."
                : "Vet doctor registered, but the credentials email could not be sent."
        });
    }

    /// <summary>
    /// Issue a new temporary password for a vet and email it. Admin only.
    /// Existing sessions are invalidated. The password is never returned.
    /// </summary>
    /// <response code="200">Credentials regenerated; <c>credentialsEmailed</c> reports delivery.</response>
    /// <response code="404">Vet or linked user not found.</response>
    [HttpPost("vets/{vetId}/reset-credentials")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetVetCredentials(Guid vetId, [FromBody] ResetCredentialsRequest? request, CancellationToken ct)
    {
        var vet = await _vetPartnerRepository.GetByIdAsync(vetId, ct);
        if (vet?.UserId is not { } userId)
            return NotFound(new { error = "Vet not found." });

        var supplied = request?.TemporaryPassword;
        if (string.IsNullOrWhiteSpace(supplied) && !_emailService.IsEnabled)
            return BadRequest(new { error = "Email delivery is disabled. Provide a temporary password to share with the vet." });

        if (!string.IsNullOrWhiteSpace(supplied) && AuthController.ValidatePassword(supplied) is { } passwordError)
            return BadRequest(new { error = passwordError });

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { error = "Vet account not found." });

        var temporaryPassword = string.IsNullOrWhiteSpace(supplied) ? GenerateTemporaryPassword() : supplied;
        user.SetTemporaryPassword(BCrypt.Net.BCrypt.HashPassword(temporaryPassword));
        user.IncrementTokenVersion();
        _unitOfWork.Users.Update(user);
        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId, ct);
        await _audit.RecordAsync("admin.vet_credentials_reset", vet.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var emailed = await _emailService.SendAsync(
            BuildCredentialsEmail(user.Email, vet.Name, temporaryPassword),
            ct);

        if (!emailed)
            _logger.LogWarning("Vet {VetId} credentials reset but email was not delivered", vetId);

        return Ok(new
        {
            credentialsEmailed = emailed,
            message = emailed
                ? "A new temporary password was emailed to the vet."
                : "Credentials were reset, but the email could not be sent."
        });
    }

    /// <summary>List all registered vets. Admin only.</summary>
    [HttpGet("vets")]
    public async Task<IActionResult> ListVets(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var vets = (await _vetPartnerRepository.GetAllAsync(ct))
            .OrderByDescending(v => v.CreatedAt)
            .ToList();

        var totalCount = vets.Count;
        var p = Math.Clamp(page, 1, int.MaxValue);
        var ps = Math.Clamp(pageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)ps);

        var pageItems = vets
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(v => new
            {
                v.Id,
                v.UserId,
                v.Name,
                v.Email,
                v.Specialization,
                v.Contact,
                v.IsActive,
                v.HasPayoutAccount,
                v.CreatedAt
            });

        return Ok(new
        {
            items = pageItems,
            page = p,
            pageSize = ps,
            totalCount,
            totalPages
        });
    }

    /// <summary>Deactivate a vet and their linked user account. Admin only.</summary>
    /// <response code="200">Vet deactivated.</response>
    /// <response code="404">Vet not found.</response>
    [HttpDelete("vets/{vetId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateVet(Guid vetId, CancellationToken ct)
    {
        var vet = await _vetPartnerRepository.GetByIdAsync(vetId, ct);
        if (vet is null)
            return NotFound(new { error = "Vet not found." });

        vet.Deactivate();
        await _audit.RecordAsync("admin.vet_deactivated", vet.Id, ct: ct);

        if (vet.UserId is { } userId)
        {
            var user = await _userRepository.GetByIdAsync(userId, ct);
            if (user is not null)
            {
                user.Deactivate();
                _unitOfWork.Users.Update(user);
            }
        }

        _vetPartnerRepository.Update(vet);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "Vet deactivated." });
    }

    /// <summary>Reactivate a vet and their linked user account.</summary>
    [HttpPost("vets/{vetId:guid}/activate")]
    public async Task<IActionResult> ActivateVet(Guid vetId, CancellationToken ct)
    {
        var vet = await _vetPartnerRepository.GetByIdAsync(vetId, ct);
        if (vet is null)
            return NotFound(new { error = "Vet not found." });

        vet.Activate();
        if (vet.UserId is { } userId && await _userRepository.GetByIdAsync(userId, ct) is { } user)
            user.Activate();
        await _audit.RecordAsync("admin.vet_activated", vet.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(new { message = "Vet reactivated." });
    }

    /// <summary>Platform overview: counts by role and status, payment totals, recent audit events and integration status.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(PlatformOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetPlatformOverviewQuery(), ct));

    /// <summary>Search users by role, name or email.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? role, [FromQuery] string? q, [FromQuery] bool? active, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetUsersQuery(role, q, active, page, pageSize), ct));

    /// <summary>Deactivate a user (ends all sessions immediately).</summary>
    [HttpPost("users/{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new SetUserActiveCommand(userId, false), ct);
        return Ok(new { message = "User deactivated." });
    }

    /// <summary>Reactivate a user.</summary>
    [HttpPost("users/{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new SetUserActiveCommand(userId, true), ct);
        return Ok(new { message = "User activated." });
    }

    /// <summary>All farms with their owners.</summary>
    [HttpGet("farms")]
    public async Task<IActionResult> Farms([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetAdminFarmsQuery(q, page, pageSize), ct));

    /// <summary>All cases across farms, filterable by status and severity.</summary>
    [HttpGet("incidents")]
    public async Task<IActionResult> Incidents([FromQuery] SearchIncidentsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>All payments, including payout amounts, for reconciliation.</summary>
    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetPaymentsQuery(null, status, page, pageSize), ct));

    /// <summary>System health: database connectivity, applied and pending migrations, and integration configuration.</summary>
    [HttpGet("system")]
    public async Task<IActionResult> SystemHealth([FromServices] FarmHandDbContext db, [FromServices] ISystemStatus status, CancellationToken ct)
    {
        var canConnect = await db.CanConnectAsync(ct);
        IEnumerable<string> applied = [], pending = [];
        if (canConnect)
        {
            applied = await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync(db.Database, ct);
            pending = await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync(db.Database, ct);
        }

        return Ok(new
        {
            database = new { connected = canConnect, appliedMigrations = applied.Count(), pendingMigrations = pending.ToList() },
            integrations = status.GetIntegrationStatus(),
            serverTimeUtc = DateTime.UtcNow,
            version = typeof(AdminController).Assembly.GetName().Version?.ToString()
        });
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";
        const string all = upper + lower + digits + symbols;

        Span<char> password = stackalloc char[14];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < password.Length; i++)
            password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Shuffle so the guaranteed character classes are not in fixed positions.
        for (var i = password.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    private static EmailMessage BuildCredentialsEmail(string email, string name, string temporaryPassword)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeEmail = WebUtility.HtmlEncode(email);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2937">
              <h2 style="color:#166534">Welcome to FarmHand</h2>
              <p>Hello {safeName},</p>
              <p>An administrator created your veterinarian account.</p>
              <table style="border-collapse:collapse;margin:16px 0">
                <tr>
                  <td style="padding:6px 12px;font-weight:bold">Email</td>
                  <td style="padding:6px 12px">{safeEmail}</td>
                </tr>
                <tr>
                  <td style="padding:6px 12px;font-weight:bold">Temporary password</td>
                  <td style="padding:6px 12px;font-family:monospace">{safePassword}</td>
                </tr>
              </table>
              <p>For your security, sign in and change this password immediately.</p>
              <p style="color:#6b7280;font-size:12px">If you did not expect this account, contact your FarmHand administrator.</p>
            </div>
            """;

        var text = $"""
            Welcome to FarmHand

            Hello {name},

            An administrator created your veterinarian account.

            Email: {email}
            Temporary password: {temporaryPassword}

            For your security, sign in and change this password immediately.
            """;

        return new EmailMessage(email, "Your FarmHand vet account", html, text);
    }
}

public record RegisterVetRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [MaxLength(100)] string? TemporaryPassword,
    [MaxLength(200)] string? FullName,
    [Required, MaxLength(100)] string Specialization,
    [Required, MaxLength(200)] string Contact,
    [MaxLength(20)] string? PhoneNumber = null);

public record ResetCredentialsRequest([MaxLength(100)] string? TemporaryPassword);
