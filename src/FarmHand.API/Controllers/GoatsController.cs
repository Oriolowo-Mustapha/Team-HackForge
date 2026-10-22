using FarmHand.Application.Features.Goats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Goat and small-livestock records.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class GoatsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GoatsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a livestock record.</summary>
    /// <remarks>
    /// The record is persisted before the new id is returned.
    /// </remarks>
    /// <param name="command">Livestock record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Record acknowledged; body contains the generated id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("records")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateLivestockRecord([FromBody] CreateLivestockRecordCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetRecords), new { }, new { id });
    }

    /// <summary>List livestock records.</summary>
    /// <remarks>Placeholder endpoint: currently returns a static message and no records.</remarks>
    /// <response code="200">Static placeholder payload.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpGet("records")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetRecords() => Ok(new { message = "Goat records endpoint" });
}
