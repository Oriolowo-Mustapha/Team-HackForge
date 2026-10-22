using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.IncidentActions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Action items for an incident.</summary>
[ApiController]
[Route("api/incidents/{incidentId}/actions")]
[Authorize]
[Produces("application/json")]
public class IncidentActionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentActionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create an action item for an incident.</summary>
    /// <param name="incidentId">Incident id; must equal the body IncidentId.</param>
    /// <param name="command">Action details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Action created; body contains the new id.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related farm.</response>
    /// <response code="404">Incident not found.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateIncidentAction(Guid incidentId, [FromBody] CreateIncidentActionCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetIncidentActions), new { incidentId }, new { id });
    }

    /// <summary>List action items for an incident.</summary>
    /// <param name="incidentId">Incident id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Action list (may be empty).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related farm.</response>
    /// <response code="404">Incident not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<IncidentActionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIncidentActions(Guid incidentId, CancellationToken ct)
    {
        var actions = await _mediator.Send(new GetIncidentActionsQuery(incidentId), ct);
        return Ok(actions);
    }
}
