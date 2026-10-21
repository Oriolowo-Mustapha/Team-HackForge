using FarmHand.Application.Features.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Payment recording.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Record a completed payment.</summary>
    /// <remarks>
    /// The endpoint trusts the client-supplied amount and transaction reference and marks the
    /// payment Completed immediately; it does not verify the reference with Nomba.
    /// Idempotency and duplicate-reference detection are not implemented.
    /// </remarks>
    /// <param name="command">Payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Payment recorded.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok();
    }
}
