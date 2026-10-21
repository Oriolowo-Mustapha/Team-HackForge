using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Features.Farms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FarmsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FarmsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFarm([FromBody] CreateFarmCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetDashboard), new { farmId = id }, new { id });
    }

    [HttpGet("{farmId}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid farmId, CancellationToken ct)
    {
        var dashboard = await _mediator.Send(new GetDashboardQuery(farmId), ct);
        if (dashboard is null) return NotFound();
        return Ok(dashboard);
    }
}
