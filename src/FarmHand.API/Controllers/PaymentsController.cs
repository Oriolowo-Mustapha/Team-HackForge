using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Payments;
using FarmHand.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace FarmHand.API.Controllers;

/// <summary>Payment initiation, verified payment recording, and Flutterwave webhooks.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMediator mediator,
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
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
    /// <response code="400">Validation failed or the amount/currency does not match the vet request.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The vet request belongs to another farm.</response>
    /// <response code="404">Vet request not found.</response>
    /// <response code="502">The payment provider is unavailable.</response>
    [HttpPost("initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new InitiatePaymentCommand(request.VetRequestId, request.Amount, request.Currency),
            ct);

        return Ok(new
        {
            transactionReference = result.TransactionReference,
            checkoutUrl = result.CheckoutUrl,
            platformCommission = result.PlatformCommission,
            totalAmount = result.TotalAmount,
            vetPayoutAmount = result.VetPayoutAmount,
            vetSubaccountUsed = result.VetSubaccountUsed
        });
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
    /// <response code="502">The payment provider is unavailable.</response>
    [HttpPost("record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentCommand command,
        CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return Ok(new { status = "paid" });
    }

    /// <summary>Flutterwave payment webhook.</summary>
    /// <remarks>
    /// Flutterwave posts charge events here. The request is authenticated by the
    /// <c>flutterwave-signature</c> (HMAC-SHA256 of the raw body) or <c>verif-hash</c> header
    /// against <c>Flutterwave:WebhookSecretHash</c>. On a successful charge the matching pending
    /// payment is marked completed; all other events are acknowledged with 200.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Webhook acknowledged.</response>
    /// <response code="401">Invalid or missing webhook signature.</response>
    /// <response code="503">Webhook verification is not configured on this server.</response>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> FlutterwaveWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        var rawBody = await new StreamReader(Request.Body, leaveOpen: true).ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var secretHash = _configuration["Flutterwave:WebhookSecretHash"] ?? string.Empty;
        var flutterwaveSignature = Request.Headers["flutterwave-signature"].FirstOrDefault();
        var verifHash = Request.Headers["verif-hash"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(secretHash))
        {
            // Fail closed: an unconfigured secret would let anyone forge a valid HMAC.
            _logger.LogError("Flutterwave webhook secret is not configured; rejecting webhook");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!_paymentService.VerifyWebhookSignature(rawBody, flutterwaveSignature, verifHash, secretHash))
        {
            _logger.LogWarning("Flutterwave webhook signature verification failed");
            return Unauthorized();
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid webhook payload." });
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventType = ReadText(root, "event") ?? string.Empty;
            if (!root.TryGetProperty("data", out var data))
                return Ok();

            var txRef = ReadText(data, "tx_ref") ?? string.Empty;
            var flutterwaveId = ReadText(data, "id") ?? string.Empty;
            var status = ReadText(data, "status") ?? string.Empty;
            var isSuccessful = string.Equals(status, "successful", StringComparison.OrdinalIgnoreCase);
            var isChargeEvent = string.Equals(eventType, "charge.completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "payment.completed", StringComparison.OrdinalIgnoreCase);

            if (isChargeEvent && isSuccessful && !string.IsNullOrWhiteSpace(flutterwaveId))
            {
                var verification = await _paymentService.VerifyPaymentAsync(flutterwaveId, ct);
                if (verification.Success)
                {
                    var payment = await _paymentRepository.GetByTransactionReferenceAsync(flutterwaveId, ct)
                        ?? (!string.IsNullOrWhiteSpace(txRef)
                            ? await _paymentRepository.GetByTransactionReferenceAsync(txRef, ct)
                            : null);

                    if (payment is not null && payment.Status != "Completed")
                    {
                        payment.MarkCompleted(flutterwaveId);
                        await _unitOfWork.SaveChangesAsync(ct);
                        _logger.LogInformation("Payment {TransactionReference} marked completed via webhook", flutterwaveId);
                    }
                }
            }
        }

        return Ok();
    }

    private static string? ReadText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}

/// <summary>Payment initiation payload.</summary>
/// <param name="VetRequestId">Vet request being paid for.</param>
/// <param name="Amount">Amount to charge.</param>
/// <param name="Currency">ISO currency code, for example "NGN".</param>
public record InitiatePaymentRequest(Guid VetRequestId, decimal Amount, string Currency);
