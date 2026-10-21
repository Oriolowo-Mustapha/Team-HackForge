using FarmHand.Application.Features.Payments;
using FarmHand.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentService _paymentService;

    public PaymentsController(IMediator mediator, IPaymentService paymentService)
    {
        _mediator = mediator;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Initiate a payment with Nomba sandbox. Returns a transaction reference.
    /// </summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken ct)
    {
        var result = await _paymentService.InitiatePaymentAsync(
            request.VetRequestId,
            request.Amount,
            request.Currency,
            ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { transactionReference = result.TransactionReference });
    }

    /// <summary>
    /// Verify and record a payment. Only marks as paid after provider verification.
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentCommand command,
        CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { status = "paid" });
    }
}

public record InitiatePaymentRequest(Guid VetRequestId, decimal Amount, string Currency);
