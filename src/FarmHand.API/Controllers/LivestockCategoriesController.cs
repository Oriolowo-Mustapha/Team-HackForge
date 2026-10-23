using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.LivestockCategories;
using FarmHand.Application.Features.Livestock;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Livestock categories for a farm.</summary>
[ApiController]
[Route("api/livestock/categories")]
[Authorize]
[Produces("application/json")]
public class LivestockCategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LivestockCategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a livestock category.</summary>
    /// <param name="command">Category details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Category created; body contains the new id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm belongs to another user.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateLivestockCategoryCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetCategories), new { farmId = command.FarmId }, new { id });
    }

    /// <summary>List livestock categories for a farm.</summary>
    /// <param name="farmId">Farm id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Category list (may be empty).</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The farm belongs to another user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<LivestockCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories(Guid farmId, CancellationToken ct)
    {
        var categories = await _mediator.Send(new GetLivestockCategoriesQuery(farmId), ct);
        return Ok(categories);
    }

    /// <summary>Groups (flocks, herds, pens) with head counts, species and registered-animal counts.</summary>
    [HttpGet("details")]
    [ProducesResponseType(typeof(IReadOnlyList<LivestockCategoryDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroups([FromQuery] Guid farmId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetLivestockGroupsQuery(farmId), ct));

    /// <summary>Rename a group or update its species and head count.</summary>
    [HttpPut("{categoryId:guid}")]
    [ProducesResponseType(typeof(LivestockCategoryDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGroup(Guid categoryId, [FromBody] UpdateGroupRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateLivestockGroupCommand(categoryId, request.Name, request.Description, request.Species, request.Headcount), ct));
}

public record UpdateGroupRequest(string Name, string? Description, string? Species, int? Headcount);
