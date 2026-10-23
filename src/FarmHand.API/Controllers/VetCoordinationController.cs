using FarmHand.Application.Features.VetCoordination;
using FarmHand.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Vet request, cost-share, and prevention-plan coordination.</summary>
/// <remarks>
/// Farm owners can create vet requests and record cost shares. Responding to a request requires
/// the Vet or Admin role, and a Vet can only respond as their own linked profile.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class VetCoordinationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IVetPartnerRepository _vetPartnerRepository;

    public VetCoordinationController(IMediator mediator, IVetPartnerRepository vetPartnerRepository)
    {
        _mediator = mediator;
        _vetPartnerRepository = vetPartnerRepository;
    }

    /// <summary>List active vets that farmers can address requests to.</summary>
    /// <param name="q">Optional filter on name or specialization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Active vet directory; contains each vet's id for request targeting.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet("vets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListAvailableVets([FromQuery] string? q, CancellationToken ct = default)
    {
        var vets = (await _vetPartnerRepository.GetAllAsync(ct))
            .Where(v => v.IsActive)
            .ToList();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim().ToLowerInvariant();
            vets = vets
                .Where(v => v.Name.ToLowerInvariant().Contains(needle)
                    || v.Specialization.ToLowerInvariant().Contains(needle))
                .ToList();
        }

        return Ok(new
        {
            items = vets
                .OrderBy(v => v.Name)
                .Select(v => new
                {
                    v.Id,
                    v.Name,
                    v.Specialization,
                    v.Contact,
                    v.HasPayoutAccount
                })
        });
    }

    /// <summary>Create a vet request for an incident.</summary>
    /// <param name="incidentId">Incident id; must equal the body IncidentId.</param>
    /// <param name="command">Request type and farmer cost.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Vet request accepted. The body contains the new request id.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("incidents/{incidentId}/vet-request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateVetRequest(Guid incidentId, [FromBody] CreateVetRequestCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        var id = await _mediator.Send(command, ct);
        return Accepted(new { id });
    }

    /// <summary>Mark a vet request as responded.</summary>
    /// <param name="vetRequestId">Vet request id; must equal the body VetRequestId.</param>
    /// <param name="command">Optional responding vet partner id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Request marked as responded.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">Vet request not found.</response>
    [HttpPost("vet-requests/{vetRequestId}/respond")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RespondVetRequest(Guid vetRequestId, [FromBody] RespondVetRequestCommand command, CancellationToken ct)
    {
        if (vetRequestId != command.VetRequestId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }

    /// <summary>Record a cost-share contribution for a vet request.</summary>
    /// <param name="vetRequestId">Vet request id; must equal the body VetRequestId.</param>
    /// <param name="command">Payer type and amount.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Cost share recorded.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("vet-requests/{vetRequestId}/cost-share")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordCostShare(Guid vetRequestId, [FromBody] RecordCostShareCommand command, CancellationToken ct)
    {
        if (vetRequestId != command.VetRequestId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }

    /// <summary>Get the cost-share contributions recorded for a vet request.</summary>
    /// <param name="vetRequestId">Vet request id; must be accessible to the caller.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Contribution ledger: items, running total, and the farmer cost target.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The vet request belongs to another farm.</response>
    /// <response code="404">Vet request not found.</response>
    [HttpGet("vet-requests/{vetRequestId}/cost-shares")]
    [ProducesResponseType(typeof(CostShareListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCostShares(Guid vetRequestId, CancellationToken ct)
    {
        var ledger = await _mediator.Send(new GetCostSharesQuery(vetRequestId), ct);
        return Ok(ledger);
    }

    /// <summary>Create a prevention plan for an incident.</summary>
    /// <param name="incidentId">Incident id; must equal the body IncidentId.</param>
    /// <param name="command">Plan content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Prevention plan created; body contains the new id.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related farm.</response>
    /// <response code="404">Incident not found.</response>
    [HttpPost("incidents/{incidentId}/prevention-plan")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePreventionPlan(Guid incidentId, [FromBody] CreatePreventionPlanCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetPreventionPlan), new { incidentId }, new { id });
    }

    /// <summary>Get the prevention plan for an incident.</summary>
    /// <param name="incidentId">Incident id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Prevention plan found.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">No prevention plan exists for this incident.</response>
    [HttpGet("incidents/{incidentId}/prevention-plan")]
    [ProducesResponseType(typeof(PreventionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreventionPlan(Guid incidentId, CancellationToken ct)
    {
        var plan = await _mediator.Send(new GetPreventionPlanQuery(incidentId), ct);
        if (plan is null) return NotFound();
        return Ok(plan);
    }
}
