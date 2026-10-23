using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.Goats;
using FarmHand.Application.Features.Livestock;
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

    /// <summary>List animals on a farm, optionally filtered by group, species or a name search.</summary>
    [HttpGet("animals")]
    [ProducesResponseType(typeof(IReadOnlyList<AnimalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAnimals(
        [FromQuery] Guid farmId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? q,
        [FromQuery] string? species,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetAnimalsQuery(farmId, categoryId, q, species, includeInactive), ct));

    /// <summary>Register an animal with its own name or tag.</summary>
    [HttpPost("animals")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAnimal([FromBody] CreateAnimalCommand command, CancellationToken ct)
    {
        var animal = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAnimal), new { animalId = animal.Id }, animal);
    }

    /// <summary>Animal profile with its observation and case history.</summary>
    [HttpGet("animals/{animalId:guid}")]
    [ProducesResponseType(typeof(AnimalDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnimal(Guid animalId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAnimalQuery(animalId), ct));

    /// <summary>Update an animal's details and history.</summary>
    [HttpPut("animals/{animalId:guid}")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAnimal(Guid animalId, [FromBody] UpdateAnimalCommand command, CancellationToken ct)
    {
        if (animalId != command.AnimalId) return BadRequest(new { error = "Route id does not match the body." });
        return Ok(await _mediator.Send(command, ct));
    }

    /// <summary>Archive an animal (sold, deceased, moved). History is kept.</summary>
    [HttpPost("animals/{animalId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid animalId, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetAnimalActiveCommand(animalId, false), ct));

    /// <summary>Restore an archived animal.</summary>
    [HttpPost("animals/{animalId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid animalId, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetAnimalActiveCommand(animalId, true), ct));

    /// <summary>Upload a profile photo (multipart field <c>image</c>; JPEG, PNG, WebP or HEIC up to 8 MB).</summary>
    [HttpPost("animals/{animalId:guid}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(Guid animalId, IFormFile image, CancellationToken ct)
    {
        var bytes = await ObservationsController.ReadAsync(image, ct);
        if (bytes is null) return BadRequest(new { error = "No image provided." });
        return Ok(await _mediator.Send(new SetAnimalPhotoCommand(animalId, bytes), ct));
    }
}
