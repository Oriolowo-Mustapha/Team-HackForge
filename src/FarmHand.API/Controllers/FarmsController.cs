using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.Farms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Farm registration and dashboard aggregation.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class FarmsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FarmsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a farm.</summary>
    /// <remarks>
    /// The farm owner is always the authenticated caller; an owner id cannot be supplied
    /// in the request body.
    /// </remarks>
    /// <param name="command">Farm details to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Farm created. The body contains the new farm id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateFarm([FromBody] CreateFarmCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetDashboard), new { farmId = id }, new { id });
    }

    /// <summary>Get a farm dashboard.</summary>
    /// <param name="farmId">Farm id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Dashboard aggregated from the farm's incidents.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own this farm.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet("{farmId}/dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(Guid farmId, CancellationToken ct)
    {
        var dashboard = await _mediator.Send(new GetDashboardQuery(farmId), ct);
        if (dashboard is null) return NotFound();
        return Ok(dashboard);
    }

    /// <summary>Farms owned by the caller, with headline counts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FarmSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetMyFarmsQuery(), ct));

    /// <summary>One farm with headline counts.</summary>
    [HttpGet("{farmId:guid}")]
    [ProducesResponseType(typeof(FarmSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid farmId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetFarmQuery(farmId), ct));

    /// <summary>Rename a farm or change its location.</summary>
    [HttpPut("{farmId:guid}")]
    [ProducesResponseType(typeof(FarmSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid farmId, [FromBody] UpdateFarmRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateFarmCommand(farmId, request.DisplayName, request.Location), ct));

    /// <summary>Farmer home dashboard: counts, cases needing attention, reminders, recent activity and the next best action.</summary>
    [HttpGet("{farmId:guid}/overview")]
    [ProducesResponseType(typeof(FarmDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(Guid farmId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetFarmOverviewQuery(farmId), ct));
}

public record UpdateFarmRequest(string DisplayName, string Location);
