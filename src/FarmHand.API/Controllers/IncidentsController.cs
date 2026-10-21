using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.Incidents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Incident intake and lifecycle management.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create an incident.</summary>
    /// <remarks>DetectedAt is set by the server; it cannot be supplied by the client.</remarks>
    /// <param name="command">Incident details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Incident created. The body contains the new incident id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetIncidents), new { farmId = command.FarmId }, new { id });
    }

    /// <summary>List incidents for a farm.</summary>
    /// <remarks>Returns every incident for the farm, newest first. No pagination is applied.</remarks>
    /// <param name="farmId">Farm id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Incident list (may be empty).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet("{farmId}")]
    [ProducesResponseType(typeof(List<IncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncidents(Guid farmId, CancellationToken ct)
    {
        var incidents = await _mediator.Send(new GetIncidentsQuery(farmId), ct);
        return Ok(incidents);
    }

    /// <summary>Transition an incident to a new status.</summary>
    /// <remarks>
    /// Send the numeric IncidentStatus value (see API.md). The route id and body IncidentId must match.
    /// Known issue: Open/unknown values are silently ignored and still return 200.
    /// </remarks>
    /// <param name="incidentId">Incident id; must equal the body IncidentId.</param>
    /// <param name="command">Target status and optional follow-up date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Status transition applied.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">Incident not found.</response>
    [HttpPost("{incidentId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid incidentId, [FromBody] UpdateIncidentStatusCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }
}
