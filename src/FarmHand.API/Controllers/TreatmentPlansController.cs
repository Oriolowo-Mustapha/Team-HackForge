using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Features.TreatmentPlans;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Treatment plans for vet requests.</summary>
[ApiController]
[Route("api/treatment-plans")]
[Authorize]
[Produces("application/json")]
public class TreatmentPlansController : ControllerBase
{
    private readonly IMediator _mediator;

    public TreatmentPlansController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Create a treatment plan for a vet request.</summary>
    /// <param name="command">Treatment plan details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Treatment plan created; body contains the new id.</response>
    /// <response code="400">Request validation failed.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="403">The caller does not own the related incident.</response>
    /// <response code="404">Vet request not found.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTreatmentPlan([FromBody] CreateTreatmentPlanCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetTreatmentPlan), new { id }, new { id });
    }

    /// <summary>Get a treatment plan by id.</summary>
    /// <param name="id">Treatment plan id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Treatment plan found.</response>
    /// <response code="401">Missing or invalid bearer token.</response>
    /// <response code="404">Treatment plan not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TreatmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTreatmentPlan(Guid id, CancellationToken ct)
    {
        var plan = await _mediator.Send(new GetTreatmentPlanQuery(id), ct);
        if (plan is null) return NotFound();
        return Ok(plan);
    }

    /// <summary>Tick off a dated treatment step (farm owner, admin or assigned vet).</summary>
    [HttpPost("{planId:guid}/steps/{stepId:guid}/complete")]
    [ProducesResponseType(typeof(TreatmentScheduleItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteStep(Guid planId, Guid stepId, CancellationToken ct) =>
        Ok(await _mediator.Send(new CompleteTreatmentStepCommand(planId, stepId), ct));
}

public record GetTreatmentPlanQuery(Guid Id) : IRequest<TreatmentPlanDto?>;

public class GetTreatmentPlanQueryHandler : IRequestHandler<GetTreatmentPlanQuery, TreatmentPlanDto?>
{
    private readonly ITreatmentPlanRepository _treatmentPlanRepository;
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetTreatmentPlanQueryHandler(
        ITreatmentPlanRepository treatmentPlanRepository,
        IVetRequestRepository vetRequestRepository,
        IResourceAuthorizationService authorization)
    {
        _treatmentPlanRepository = treatmentPlanRepository;
        _vetRequestRepository = vetRequestRepository;
        _authorization = authorization;
    }

    public async Task<TreatmentPlanDto?> Handle(GetTreatmentPlanQuery request, CancellationToken ct)
    {
        var plan = await _treatmentPlanRepository.GetByIdAsync(request.Id, ct);
        if (plan is null) return null;

        var vetRequest = await _vetRequestRepository.GetByIdAsync(plan.VetRequestId, ct);
        if (vetRequest is null) return null;

        await _authorization.EnsureIncidentAccessAsync(vetRequest.IncidentId, ct);

        return new TreatmentPlanDto(plan.Id, plan.VetRequestId, plan.AuthoredByVetId, plan.InstructionsText, plan.Language);
    }
}
