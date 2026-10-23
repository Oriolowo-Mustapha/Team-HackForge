using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Features.Observations;
using FarmHand.Application.Features.Poultry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FarmHand.API.Controllers;

/// <summary>Health reports (observations): submit by text, voice or photo, review, confirm or discard.</summary>
[ApiController]
[Route("api/observations")]
[Authorize]
[Produces("application/json")]
public class ObservationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ObservationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>List reports for the caller's farms (or one farm), newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ObservationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? farmId,
        [FromQuery] ObservationConfirmationStatus? status,
        [FromQuery] Guid? animalId,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _mediator.Send(new GetObservationsQuery(farmId, status, animalId, categoryId, page, pageSize), ct));
    }

    /// <summary>Get one report with its extracted fields, photo analysis and assessment.</summary>
    [HttpGet("{observationId:guid}")]
    [ProducesResponseType(typeof(ObservationDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid observationId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetObservationQuery(observationId), ct));

    /// <summary>
    /// Submit a health report as multipart/form-data: <c>text</c>, <c>audio</c> (voice note), <c>image</c> (photo),
    /// or <c>manualEntry=true</c> with structured fields. The report is saved as pending for review.
    /// </summary>
    /// <remarks>
    /// Send an <c>idempotencyKey</c> (or the <c>Idempotency-Key</c> header) so retries never create duplicates.
    /// If the AI provider is unavailable the response is 503 with <c>code: "ai_unavailable"</c>; resubmit with
    /// <c>manualEntry=true</c> to save the report without AI.
    /// </remarks>
    /// <response code="201">Report saved as pending.</response>
    /// <response code="200">A report with this idempotency key already exists; it is returned.</response>
    /// <response code="400">Validation failed or the file type/size is not supported.</response>
    /// <response code="503">The AI or transcription provider is unavailable (<c>code: ai_unavailable</c>).</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ai")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ObservationDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit([FromForm] SubmitObservationForm form, CancellationToken ct)
    {
        var key = form.IdempotencyKey ?? Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            throw PublicException.BadRequest("An idempotency key is required.");

        var result = await _mediator.Send(new SubmitObservationCommand(
            form.FarmId,
            form.LivestockAnimalId,
            form.LivestockCategoryId,
            form.Text,
            await ReadAsync(form.Audio, ct),
            form.Audio?.FileName,
            await ReadAsync(form.Image, ct),
            key,
            form.ManualEntry,
            form.MortalityCount,
            form.WaterConsumption,
            form.FeedConsumption,
            form.EggProduction), ct);

        return result.Reused ? Ok(result) : CreatedAtAction(nameof(Get), new { observationId = result.Id }, result);
    }

    /// <summary>Confirm a pending report (optionally with corrections). Assesses severity and opens a case for High/Critical.</summary>
    [HttpPost("{observationId:guid}/confirm")]
    [EnableRateLimiting("ai")]
    [ProducesResponseType(typeof(ConfirmObservationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid observationId, [FromBody] ConfirmObservationRequest request, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new ConfirmObservationCommand(
            observationId,
            request.FarmerNotes,
            request.OverrideSeverity,
            request.OverrideReason,
            request.MortalityCount,
            request.WaterConsumption,
            request.FeedConsumption,
            request.EggProduction), ct));
    }

    /// <summary>Discard a pending report that has not been confirmed.</summary>
    [HttpDelete("{observationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Discard(Guid observationId, CancellationToken ct)
    {
        await _mediator.Send(new DiscardObservationCommand(observationId), ct);
        return NoContent();
    }

    internal static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;
        if (file.Length > 12 * 1024 * 1024)
            throw PublicException.BadRequest("The file is too large.");
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}

public class SubmitObservationForm
{
    public Guid FarmId { get; set; }
    public Guid? LivestockAnimalId { get; set; }
    public Guid? LivestockCategoryId { get; set; }
    public string? Text { get; set; }
    public IFormFile? Audio { get; set; }
    public IFormFile? Image { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool ManualEntry { get; set; }
    public int? MortalityCount { get; set; }
    public string? WaterConsumption { get; set; }
    public string? FeedConsumption { get; set; }
    public string? EggProduction { get; set; }
}

public record ConfirmObservationRequest(
    string? FarmerNotes,
    Severity? OverrideSeverity = null,
    string? OverrideReason = null,
    int? MortalityCount = null,
    string? WaterConsumption = null,
    string? FeedConsumption = null,
    string? EggProduction = null);
