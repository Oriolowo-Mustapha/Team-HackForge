using FarmHand.Application.Features.Poultry;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PoultryController : ControllerBase
{
    private readonly IMediator _mediator;

    public PoultryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("observations")]
    public async Task<IActionResult> RecordObservation([FromBody] RecordObservationCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return Accepted(new { id });
    }

    [HttpPost("observations/{observationId}/confirm")]
    public async Task<IActionResult> ConfirmObservation(Guid observationId, [FromBody] ConfirmObservationCommand command, CancellationToken ct)
    {
        if (observationId != command.ObservationId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }
}
