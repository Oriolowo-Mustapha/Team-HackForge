using FarmHand.Application.Features.Poultry;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Poultry observation intake and farmer confirmation.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PoultryController : ControllerBase
{
    private readonly IMediator _mediator;

    public PoultryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Record a poultry observation from free text.</summary>
    /// <remarks>
    /// The raw text is sent to Gemini (falling back to Groq) for structured extraction.
    /// idempotencyKey is required by validation but is not yet persisted or used for de-duplication.
    /// </remarks>
    /// <param name="command">Flock id, raw farmer text, idempotency key, and optional image URL.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="202">Observation accepted. The body contains the new observation id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("observations")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordObservation([FromBody] RecordObservationCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Accepted(new { id });
    }

    /// <summary>Confirm an AI-extracted observation.</summary>
    /// <remarks>The route id and body ObservationId must match. correctedNotes replaces the farmer notes when supplied.</remarks>
    /// <param name="observationId">Observation id; must equal the body ObservationId.</param>
    /// <param name="command">Confirmation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Observation confirmed.</response>
    /// <response code="400">Route id does not match the body, or validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">Observation not found.</response>
    [HttpPost("observations/{observationId}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmObservation(Guid observationId, [FromBody] ConfirmObservationCommand command, CancellationToken ct)
    {
        if (observationId != command.ObservationId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }
}
