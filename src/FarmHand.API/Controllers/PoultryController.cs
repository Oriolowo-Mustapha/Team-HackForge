using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Observations;
using FarmHand.Application.Features.Poultry;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmHand.API.Controllers;

/// <summary>Legacy JSON observation routes. New clients use <c>/api/observations</c>.</summary>
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

    /// <summary>Record an observation from text. Supplying <c>preExtracted</c> stores those fields as a manual entry.</summary>
    [HttpPost("observations")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> RecordObservation([FromBody] LegacyObservationRequest request, CancellationToken ct)
    {
        var manual = request.PreExtracted is not null;
        var result = await _mediator.Send(new SubmitObservationCommand(
            request.FarmId,
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            request.RawText,
            null, null, null,
            request.IdempotencyKey,
            manual,
            request.PreExtracted?.MortalityCount,
            request.PreExtracted?.WaterConsumption,
            request.PreExtracted?.FeedConsumption,
            request.PreExtracted?.EggProduction), ct);
        return Accepted(new { id = result.Id });
    }

    /// <summary>Confirm a pending observation.</summary>
    [HttpPost("observations/{observationId}/confirm")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> ConfirmObservation(Guid observationId, [FromBody] ConfirmObservationCommand command, CancellationToken ct)
    {
        if (observationId != command.ObservationId) return BadRequest();
        return Ok(await _mediator.Send(command, ct));
    }
}

public record LegacyObservationRequest(
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string RawText,
    string IdempotencyKey,
    ExtractedObservation? PreExtracted = null);
