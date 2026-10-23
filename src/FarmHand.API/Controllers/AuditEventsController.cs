using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.AuditEvents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Audit events.</summary>
[ApiController]
[Route("api/audit-events")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AuditEventsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditEventsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Record an audit event.</summary>
    /// <param name="command">Audit event details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Audit event recorded; body contains the new id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAuditEvent([FromBody] CreateAuditEventCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAuditEvents), new { actorId = command.ActorId }, new { id });
    }

    /// <summary>List audit events, optionally filtered by actor or event type.</summary>
    /// <param name="actorId">Optional actor id filter.</param>
    /// <param name="eventType">Optional event type filter.</param>
    /// <param name="relatedRecordId">Optional related record filter.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Page size (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Audit event list (may be empty).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAuditEvents(string? actorId, string? eventType, Guid? relatedRecordId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var events = await _mediator.Send(new GetAuditEventsQuery(actorId, eventType, relatedRecordId, page, pageSize), ct);
        return Ok(events);
    }
}
