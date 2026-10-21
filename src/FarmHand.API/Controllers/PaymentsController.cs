using FarmHand.Application.Features.Payments;
using FarmHand.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Payment initiation and verified payment recording.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentService _paymentService;

    public PaymentsController(IMediator mediator, IPaymentService paymentService)
    {
        _mediator = mediator;
        _paymentService = paymentService;
    }

    /// <summary>Initiate a payment and obtain a transaction reference.</summary>
    /// <remarks>
    /// Runs against the Nomba integration configured under <c>Nomba</c>. The reference must be
    /// passed to <c>POST /api/payments/record</c> after the payer completes payment.
    /// </remarks>
    /// <param name="request">Vet request, amount, and currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Reference created; body contains <c>transactionReference</c>.</response>
    /// <response code="400">Provider rejected the initiation request.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    [HttpPost("initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Verify a payment with the provider and record it.</summary>
    /// <remarks>
    /// Calls the provider's verification endpoint before marking the payment completed. If
    /// <c>Nomba:ApiKey</c> is missing, verification fails and the request returns 500.
    /// </remarks>
    /// <param name="command">Vet request, amount, and provider transaction reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Payment verified and recorded.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="500">Provider verification failed or was not configured.</response>
    [HttpPost("record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentCommand command,
        CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { status = "paid" });
    }
}

/// <summary>Payment initiation payload.</summary>
/// <param name="VetRequestId">Vet request being paid for.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">ISO currency code, for example "NGN".</param>
public record InitiatePaymentRequest(Guid VetRequestId, decimal Amount, string Currency);
