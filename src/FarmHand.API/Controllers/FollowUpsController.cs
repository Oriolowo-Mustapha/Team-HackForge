using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.FollowUps;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Follow-up records for incidents.</summary>
[ApiController]
[Route("api/follow-ups")]
[Authorize]
[Produces("application/json")]
public class FollowUpsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FollowUpsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Record a follow-up for an incident.</summary>
    /// <param name="command">Follow-up details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Follow-up created; body contains the new id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related farm.</response>
    /// <response code="404">Incident not found.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetFollowUps), new { incidentId = command.IncidentId }, new { id });
    }

    /// <summary>List follow-ups for an incident.</summary>
    /// <param name="incidentId">Incident id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Follow-up list (may be empty).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related farm.</response>
    /// <response code="404">Incident not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<FollowUpDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowUps(Guid incidentId, CancellationToken ct)
    {
        var followUps = await _mediator.Send(new GetFollowUpsQuery(incidentId), ct);
        return Ok(followUps);
    }
}
