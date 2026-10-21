using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    /// Creates both a User account (Vet role) and a VetPartner profile.
    /// </summary>
    [HttpPost("vets")]
    public async Task<IActionResult> RegisterVet([FromBody] RegisterVetRequest request, CancellationToken ct)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, ct))
            return BadRequest(new { error = "Email already registered." });

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword);

        var vetUser = new User(
            request.Email,
            passwordHash,
            "Vet",
            request.FullName,
            request.PhoneNumber);

        await _unitOfWork.Users.AddAsync(vetUser, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var vetPartner = new VetPartner(
            request.FullName ?? request.Email,
            request.Specialization,
            request.Contact);

        await _vetPartnerRepository.AddAsync(vetPartner, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(RegisterVet), new { }, new
        {
            vetId = vetPartner.Id,
            userId = vetUser.Id,
            email = vetUser.Email,
            role = vetUser.Role,
            specialization = vetPartner.Specialization,
            message = "Vet doctor registered successfully."
        });
    }

    /// <summary>
    /// List all registered vets. Admin only.
    /// </summary>
    [HttpGet("vets")]
    public async Task<IActionResult> ListVets(CancellationToken ct)
    {
        var vets = await _vetPartnerRepository.GetAllAsync(ct);
        return Ok(vets.Select(v => new
        {
            v.Id,
            v.Name,
            v.Specialization,
            v.Contact,
            v.IsActive,
            v.CreatedAt
        }));
    }

    /// <summary>
    /// Deactivate a vet. Admin only.
    /// </summary>
    [HttpDelete("vets/{vetId}")]
    public async Task<IActionResult> DeactivateVet(Guid vetId, CancellationToken ct)
    {
        var vet = await _vetPartnerRepository.GetByIdAsync(vetId, ct);
        if (vet is null)
            return NotFound(new { error = "Vet not found." });

        // Deactivate the associated user account
        var user = await _userRepository.GetByEmailAsync(vet.Name, ct);
        if (user is not null)
        {
            user.Deactivate();
            _unitOfWork.Users.Update(user);
        }

        vet.GetType().GetProperty("IsActive")!.SetValue(vet, false);
        _vetPartnerRepository.Update(vet);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "Vet deactivated." });
    }
}

public record RegisterVetRequest(
    string Email,
    string TemporaryPassword,
    string FullName,
    string Specialization,
    string Contact,
    string? PhoneNumber = null);

// Need IVetPartnerRepository - let me check if it exists
// If not, we need to add it
