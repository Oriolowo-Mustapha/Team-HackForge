using FarmHand.Application.Features.Incidents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetIncidents), new { farmId = command.FarmId }, new { id });
    }

    [HttpGet("{farmId}")]
    public async Task<IActionResult> GetIncidents(Guid farmId, CancellationToken ct)
    {
        var incidents = await _mediator.Send(new GetIncidentsQuery(farmId), ct);
        return Ok(incidents);
    }

    [HttpPost("{incidentId}/status")]
    public async Task<IActionResult> UpdateStatus(Guid incidentId, [FromBody] UpdateIncidentStatusCommand command, CancellationToken ct)
    {
        if (incidentId != command.IncidentId) return BadRequest();
        await _mediator.Send(command, ct);
        return Ok();
    }
}
