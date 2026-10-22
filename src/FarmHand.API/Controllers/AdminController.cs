using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminController(
        IUserRepository userRepository,
        IVetPartnerRepository vetPartnerRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _vetPartnerRepository = vetPartnerRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Register a vet doctor on the system. Admin only.
    /// Creates the User account and VetPartner profile in one transaction.
    /// </summary>
    /// <response code="201">Vet registered with linked user id.</response>
    /// <response code="400">Validation failed or email already registered.</response>
    [HttpPost("vets")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterVet([FromBody] RegisterVetRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.ExistsByEmailAsync(email, ct))
            return BadRequest(new { error = "Email already registered." });

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8)
            return BadRequest(new { error = "Temporary password must be at least 8 characters." });

        if (System.Text.Encoding.UTF8.GetByteCount(request.TemporaryPassword) > 72)
            return BadRequest(new { error = "Temporary password must be at most 72 bytes." });

        var vetUser = new User(
            email,
            BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword),
            "Vet",
            request.FullName?.Trim(),
            request.PhoneNumber?.Trim());

        var vetPartner = new VetPartner(
            vetUser.Id,
            request.FullName?.Trim() ?? email,
            email,
            request.Specialization,
            request.Contact);

        await _unitOfWork.Users.AddAsync(vetUser, ct);
        await _unitOfWork.VetPartners.AddAsync(vetPartner, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { error = "Email already registered." });
        }

        return CreatedAtAction(nameof(ListVets), new { }, new
        {
            vetId = vetPartner.Id,
            userId = vetUser.Id,
            email = vetUser.Email,
            role = vetUser.Role,
            specialization = vetPartner.Specialization,
            message = "Vet doctor registered successfully."
        });
    }

    /// <summary>List all registered vets. Admin only.</summary>
    [HttpGet("vets")]
    public async Task<IActionResult> ListVets(CancellationToken ct)
    {
        var vets = await _vetPartnerRepository.GetAllAsync(ct);
        return Ok(vets.Select(v => new
        {
            v.Id,
            v.UserId,
            v.Name,
            v.Email,
            v.Specialization,
            v.Contact,
            v.IsActive,
            v.CreatedAt
        }));
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
}

public record RegisterVetRequest(
    [property: Required, EmailAddress, MaxLength(255)] string Email,
    [property: Required] string TemporaryPassword,
    [property: MaxLength(200)] string? FullName,
    [property: Required, MaxLength(100)] string Specialization,
    [property: Required, MaxLength(200)] string Contact,
    [property: MaxLength(20)] string? PhoneNumber = null);
