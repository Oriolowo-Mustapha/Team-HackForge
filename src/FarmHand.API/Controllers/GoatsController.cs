using FarmHand.Application.Features.Goats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoatsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GoatsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateLivestockRecord([FromBody] CreateLivestockRecordCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetRecords), new { }, new { id });
    }

    [HttpGet("records")]
    public IActionResult GetRecords() => Ok(new { message = "Goat records endpoint" });
}
