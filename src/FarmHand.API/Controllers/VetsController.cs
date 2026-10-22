using FarmHand.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Directory of active veterinary partners for selecting a vet on a request.</summary>
[ApiController]
[Route("api/vets")]
[Authorize]
[Produces("application/json")]
public class VetsController : ControllerBase
{
    private readonly IVetPartnerRepository _vetPartnerRepository;

    public VetsController(IVetPartnerRepository vetPartnerRepository)
    {
        _vetPartnerRepository = vetPartnerRepository;
    }

    /// <summary>List active vets that can be assigned to a visit.</summary>
    /// <response code="200">Active vets returned.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var vets = await _vetPartnerRepository.GetActiveAsync(ct);

        return Ok(vets
            .OrderBy(v => v.Name)
            .Select(v => new
            {
                v.Id,
                v.Name,
                v.Specialization,
                v.Contact,
                v.HasPayoutAccount
            }));
    }
}
