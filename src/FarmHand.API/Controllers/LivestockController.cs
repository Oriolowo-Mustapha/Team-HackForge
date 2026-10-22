using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.Goats;
using FarmHand.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Livestock records (animals) for a farm.</summary>
[ApiController]
[Route("api/livestock")]
[Authorize]
[Produces("application/json")]
public class LivestockController : ControllerBase
{
    private readonly IMediator _mediator;

    public LivestockController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a livestock record.</summary>
    /// <remarks>
    /// Species and AnimalName are chosen from fixed dropdown enums.
    /// </remarks>
    /// <param name="command">Livestock record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Record created; body contains the generated id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm belongs to another user.</response>
    /// <response code="404">Category not found.</response>
    [HttpPost("records")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateLivestockRecord([FromBody] CreateLivestockRecordCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetRecords), new { farmId = command.FarmId }, new { id });
    }

    /// <summary>List livestock records for a farm.</summary>
    /// <param name="farmId">Farm id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Livestock records for the farm.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own this farm.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet("records")]
    [ProducesResponseType(typeof(List<LivestockRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRecords(Guid farmId, CancellationToken ct)
    {
        var records = await _mediator.Send(new GetLivestockRecordsQuery(farmId), ct);
        return Ok(records);
    }

    /// <summary>Available livestock species (dropdown options).</summary>
    /// <response code="200">List of species enum values.</response>
    [HttpGet("species")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LivestockSpecies[]), StatusCodes.Status200OK)]
    public IActionResult GetSpecies() => Ok(Enum.GetValues<LivestockSpecies>());

    /// <summary>Available animal names/tags (dropdown options).</summary>
    /// <response code="200">List of animal name enum values.</response>
    [HttpGet("names")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AnimalName[]), StatusCodes.Status200OK)]
    public IActionResult GetNames() => Ok(Enum.GetValues<AnimalName>());
}
