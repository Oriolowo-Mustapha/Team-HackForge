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
    /// <remarks>Returns incidents for the farm, newest first, with pagination.</remarks>
    /// <param name="farmId">Farm id.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Paginated incident list.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet("{farmId}")]
    [ProducesResponseType(typeof(PagedResult<IncidentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncidents(Guid farmId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var incidents = await _mediator.Send(new GetIncidentsQuery(farmId, page, pageSize), ct);
        return Ok(incidents);
    }

    /// <summary>Transition an incident to a new status.</summary>
    /// <remarks>
    /// Send the numeric IncidentStatus value (see API.md). The route id and body IncidentId must match.
    /// Open (0) and unknown values are rejected with 400.
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

    /// <summary>Search cases visible to the caller (farmer: own farms; vet: assigned cases; admin: all).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchIncidentsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    /// <summary>Full case file: subject, source report, assessment, vet coordination, plans, follow-ups, actions and timeline.</summary>
    [HttpGet("{incidentId:guid}/case")]
    [ProducesResponseType(typeof(IncidentCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCase(Guid incidentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetIncidentCaseQuery(incidentId), ct));
}
