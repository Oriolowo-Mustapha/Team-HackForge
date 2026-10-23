using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.TreatmentPlans;

public record TreatmentScheduleInput(string Action, DateTime DueDate);

/// <summary>
/// The assigned vet (or an admin acting for a vet) records treatment instructions for an accepted request,
/// optionally with dated steps the farmer can tick off.
/// </summary>
/// <param name="VetRequestId">Accepted vet request the plan belongs to.</param>
/// <param name="AuthoredByVetId">Ignored for vets (their own profile is used); admins may name the authoring vet.</param>
/// <param name="InstructionsText">Care instructions for the farmer.</param>
/// <param name="Language">Optional language code, e.g. "en", "yo", "pcm".</param>
/// <param name="Schedule">Optional dated treatment steps.</param>
public record CreateTreatmentPlanCommand(
    Guid VetRequestId,
    string? AuthoredByVetId,
    string InstructionsText,
    string? Language,
    IReadOnlyList<TreatmentScheduleInput>? Schedule = null) : IRequest<Guid>;

public class CreateTreatmentPlanCommandHandler : IRequestHandler<CreateTreatmentPlanCommand, Guid>
{
    private readonly ITreatmentPlanRepository _treatmentPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public CreateTreatmentPlanCommandHandler(
        ITreatmentPlanRepository treatmentPlanRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser,
        IAuditLogger audit)
    {
        _treatmentPlanRepository = treatmentPlanRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreateTreatmentPlanCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");

        string authoredByVetId;
        if (_currentUser.IsAdmin)
        {
            authoredByVetId = vetRequest.VetPartnerId?.ToString()
                ?? throw PublicException.Conflict("Assign a vet before adding a treatment plan.");
        }
        else
        {
            var vet = await _authorization.GetCurrentVetAsync(ct)
                ?? throw PublicException.Forbidden("Only the assigned veterinarian can write a treatment plan.");
            if (vetRequest.VetPartnerId != vet.Id)
                throw PublicException.Forbidden("This request is assigned to another vet.");
            authoredByVetId = vet.Id.ToString();
        }

        if (vetRequest.Status is not (RequestStatus.Responded or RequestStatus.Completed))
            throw PublicException.Conflict("Accept the request before writing a treatment plan.");

        var plan = new TreatmentPlan(request.VetRequestId, authoredByVetId, request.InstructionsText.Trim(), request.Language);
        await _treatmentPlanRepository.AddAsync(plan, ct);

        foreach (var step in request.Schedule ?? [])
            await _treatmentPlanRepository.AddScheduleItemAsync(new TreatmentScheduleItem(plan.Id, step.Action.Trim(), DateTime.SpecifyKind(step.DueDate, DateTimeKind.Utc)), ct);

        await _audit.RecordAsync("treatment_plan.created", vetRequest.IncidentId, $"plan={plan.Id}", ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return plan.Id;
    }
}

public class CreateTreatmentPlanCommandValidator : AbstractValidator<CreateTreatmentPlanCommand>
{
    public CreateTreatmentPlanCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.AuthoredByVetId).MaximumLength(255);
        RuleFor(x => x.InstructionsText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Language).MaximumLength(10);
        RuleFor(x => x.Schedule).Must(s => s is null || s.Count <= 20).WithMessage("A plan can have at most 20 scheduled steps.");
        RuleForEach(x => x.Schedule).ChildRules(step =>
        {
            step.RuleFor(s => s.Action).NotEmpty().MaximumLength(200);
            step.RuleFor(s => s.DueDate).GreaterThan(new DateTime(2000, 1, 1));
        });
    }
}

/// <summary>Marks a treatment step done (farm owner, admin or assigned vet).</summary>
public record CompleteTreatmentStepCommand(Guid PlanId, Guid StepId) : IRequest<TreatmentScheduleItemDto>;

public class CompleteTreatmentStepCommandHandler : IRequestHandler<CompleteTreatmentStepCommand, TreatmentScheduleItemDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CompleteTreatmentStepCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<TreatmentScheduleItemDto> Handle(CompleteTreatmentStepCommand request, CancellationToken ct)
    {
        var plan = await _unitOfWork.TreatmentPlans.GetByIdAsync(request.PlanId, ct)
            ?? throw PublicException.NotFound("Treatment plan not found.");
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(plan.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        await _authorization.EnsureIncidentWriteAccessAsync(vetRequest.IncidentId, ct);

        var step = await _unitOfWork.TreatmentPlans.GetScheduleItemAsync(request.StepId, ct);
        if (step is null || step.TreatmentPlanId != plan.Id)
            throw PublicException.NotFound("Treatment step not found.");

        if (!step.IsCompleted)
        {
            step.MarkCompleted();
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new TreatmentScheduleItemDto(step.Id, step.Action, step.DueDate, step.IsCompleted, step.CompletedAt);
    }
}
