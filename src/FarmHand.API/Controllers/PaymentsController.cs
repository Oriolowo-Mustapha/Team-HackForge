using FarmHand.Application.Features.Payments;
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

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Initiate a payment and obtain a transaction reference.</summary>
    /// <remarks>
    /// Creates a pending payment owned by the caller and obtains a reference from Flutterwave
    /// (or the signed sandbox reference when <c>Flutterwave:Mode</c> is not <c>Live</c>).
    /// The response includes <c>checkoutUrl</c> which the caller should redirect the payer to.
    /// The reference must be passed to <c>POST /api/payments/record</c> after payment.
    /// </remarks>
    /// <param name="request">Vet request, amount, and currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Reference created; body contains <c>transactionReference</c> and <c>checkoutUrl</c>.</response>
    /// <response code="400">Provider rejected the initiation request.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The vet request belongs to another farm.</response>
    /// <response code="404">Vet request not found.</response>
    [HttpPost("initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new InitiatePaymentCommand(request.VetRequestId, request.Amount, request.Currency),
            ct);

        return Ok(new { transactionReference = result.TransactionReference, checkoutUrl = result.CheckoutUrl });
    }

    /// <summary>Verify a payment with the provider and record it.</summary>
    /// <remarks>
    /// The reference must already exist as a pending payment for this vet request. Amount and
    /// currency must match the initiated payment. Repeating a successful record is idempotent.
    /// </remarks>
    /// <param name="command">Vet request, amount, currency, and provider reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Payment verified and recorded.</response>
    /// <response code="400">Validation failed or amount/currency mismatch.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The vet request belongs to another farm.</response>
    /// <response code="404">Payment reference or vet request not found.</response>
    /// <response code="409">Payment could not be completed.</response>
    [HttpPost("record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
