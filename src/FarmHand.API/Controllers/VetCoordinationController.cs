using FarmHand.Application.Features.VetCoordination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VetCoordinationController : ControllerBase
{
    private readonly IMediator _mediator;

    public VetCoordinationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("incidents/{incidentId}/vet-request")]
    public async Task<IActionResult> CreateVetRequest(Guid incidentId, [FromBody] CreateVetRequestCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        var id = await _mediator.Send(command, ct);
        return Accepted(new { id });
    }

    [HttpPost("vet-requests/{vetRequestId}/respond")]
    public async Task<IActionResult> RespondVetRequest(Guid vetRequestId, [FromBody] RespondVetRequestCommand command, CancellationToken ct)
    {
        if (vetRequestId != command.VetRequestId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }

    [HttpPost("vet-requests/{vetRequestId}/cost-share")]
    public async Task<IActionResult> RecordCostShare(Guid vetRequestId, [FromBody] RecordCostShareCommand command, CancellationToken ct)
    {
        if (vetRequestId != command.VetRequestId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }

    [HttpGet("incidents/{incidentId}/prevention-plan")]
    public async Task<IActionResult> GetPreventionPlan(Guid incidentId, CancellationToken ct)
    {
        var plan = await _mediator.Send(new GetPreventionPlanQuery(incidentId), ct);
        if (plan is null) return NotFound();
        return Ok(new { content = plan });
    }
}
