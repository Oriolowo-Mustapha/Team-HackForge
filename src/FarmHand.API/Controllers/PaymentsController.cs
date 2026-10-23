using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace FarmHand.API.Controllers;

/// <summary>
/// Payments for accepted vet requests. A payment is only marked paid after the server verifies it
/// with Flutterwave (or, in explicitly configured simulated mode, after a labelled simulated confirmation).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[EnableRateLimiting("payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMediator mediator,
        IPaymentService paymentService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _paymentService = paymentService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Pre-payment summary: vet fee, 5% platform commission, total, currency and payment mode.</summary>
    [HttpGet("quote")]
    [ProducesResponseType(typeof(PaymentQuoteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote([FromQuery] Guid vetRequestId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetPaymentQuoteQuery(vetRequestId), ct));

    /// <summary>Payment history (farmer: own farms; vet: assigned work with payout amounts; admin: all).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? vetRequestId, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetPaymentsQuery(vetRequestId, status, page, pageSize), ct));

    /// <summary>One payment by its reference.</summary>
    [HttpGet("{reference}")]
    public async Task<IActionResult> Get(string reference, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetPaymentQuery(reference), ct));

    /// <summary>Start a checkout for an accepted vet request.</summary>
    /// <remarks>
    /// The response includes <c>checkoutUrl</c> (live mode) to redirect the payer to, and <c>mode</c>.
    /// In simulated mode there is no checkout URL and no money moves. Pass an <c>Idempotency-Key</c>
    /// header to make retries safe; an interrupted checkout is resumed instead of duplicated.
    /// </remarks>
    /// <response code="200">Checkout created or resumed.</response>
    /// <response code="409">Not payable yet (no vet accepted), already paid, or a different checkout is pending.</response>
    /// <response code="503">Payments are not configured on this server.</response>
    [HttpPost("initiate")]
    public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await _mediator.Send(
            new InitiatePaymentCommand(
                request.VetRequestId,
                request.Amount,
                request.Currency,
                IdempotencyKey: string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()),
            ct);

        return Ok(new
        {
            paymentId = result.PaymentId,
            status = result.Status,
            mode = result.Mode,
            transactionReference = result.TransactionReference,
            checkoutUrl = result.CheckoutUrl,
            platformCommission = result.PlatformCommission,
            totalAmount = result.TotalAmount,
            vetPayoutAmount = result.VetPayoutAmount,
            vetSubaccountUsed = result.VetSubaccountUsed,
            subaccountId = result.SubaccountId,
            idempotencyKey = result.IdempotencyKey
        });
    }

    /// <summary>
    /// Ask the server to verify a payment with the provider (used after returning from checkout, and to
    /// confirm a simulated payment). The result reflects the provider, never the client.
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify([FromBody] VerifyPaymentRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new VerifyPaymentCommand(request.TransactionReference), ct));

    /// <summary>Cancel a pending checkout so a new one can be started. Live payments are re-verified first.</summary>
    [HttpPost("{reference}/cancel")]
    [ProducesResponseType(typeof(PaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(string reference, CancellationToken ct) =>
        Ok(await _mediator.Send(new CancelPaymentCommand(reference), ct));

    /// <summary>Legacy: verify a payment while asserting the expected amount and currency.</summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(new { status = result.Payment.Status == "Completed" ? "paid" : result.Payment.Status.ToLowerInvariant(), message = result.Message, payment = result.Payment });
    }

    /// <summary>Flutterwave payment webhook.</summary>
    /// <remarks>
    /// Authenticated by the <c>flutterwave-signature</c> (HMAC-SHA256 of the raw body) or <c>verif-hash</c>
    /// header against <c>Flutterwave:WebhookSecretHash</c>. Amounts in the body are ignored: the transaction
    /// is re-verified with Flutterwave. Duplicate deliveries are acknowledged without side effects.
    /// </remarks>
    /// <response code="200">Webhook acknowledged.</response>
    /// <response code="401">Invalid or missing webhook signature.</response>
    /// <response code="503">Webhook verification is not configured on this server.</response>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [DisableRateLimiting]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> FlutterwaveWebhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        var rawBody = await new StreamReader(Request.Body, leaveOpen: true).ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var secretHash = _configuration["Flutterwave:WebhookSecretHash"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(secretHash) || secretHash.StartsWith("replace-", StringComparison.OrdinalIgnoreCase))
        {
            // Fail closed: an unconfigured secret would let anyone forge a valid HMAC.
            _logger.LogError("Flutterwave webhook secret is not configured; rejecting webhook");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var flutterwaveSignature = Request.Headers["flutterwave-signature"].FirstOrDefault();
        var verifHash = Request.Headers["verif-hash"].FirstOrDefault();
        if (!_paymentService.VerifyWebhookSignature(rawBody, flutterwaveSignature, verifHash, secretHash))
        {
            _logger.LogWarning("Flutterwave webhook signature verification failed");
            return Unauthorized();
        }

        string eventType, transactionId, reference, status;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            eventType = ReadText(root, "event") ?? ReadText(root, "type") ?? string.Empty;
            if (!root.TryGetProperty("data", out var data))
                return Ok(new { result = "ignored" });
            transactionId = ReadText(data, "id") ?? string.Empty;
            reference = ReadText(data, "tx_ref") ?? ReadText(data, "reference") ?? string.Empty;
            status = ReadText(data, "status") ?? string.Empty;
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "Invalid webhook payload." });
        }

        var result = await _mediator.Send(new ProcessPaymentWebhookCommand(eventType.ToLowerInvariant(), transactionId, reference, status), ct);
        _logger.LogInformation("Flutterwave webhook processed with result {Result}", result);
        return Ok(new { result });
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
/// <param name="Amount">Vet fee (must equal the request's fee).</param>
/// <param name="Currency">ISO currency code, for example "NGN".</param>
public record InitiatePaymentRequest(Guid VetRequestId, decimal Amount, string Currency);

public record VerifyPaymentRequest(string TransactionReference);
