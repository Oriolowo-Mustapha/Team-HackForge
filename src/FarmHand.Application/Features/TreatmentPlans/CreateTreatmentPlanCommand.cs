using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.TreatmentPlans;

/// <summary>Create a treatment plan for a vet request.</summary>
/// <param name="VetRequestId">Vet request id; must be accessible to the caller.</param>
/// <param name="AuthoredByVetId">Vet id authoring the plan.</param>
/// <param name="InstructionsText">Care instructions.</param>
/// <param name="Language">Language of the instructions.</param>
public record CreateTreatmentPlanCommand(Guid VetRequestId, string AuthoredByVetId, string InstructionsText, string? Language) : IRequest<Guid>;

public class CreateTreatmentPlanCommandHandler : IRequestHandler<CreateTreatmentPlanCommand, Guid>
{
    private readonly ITreatmentPlanRepository _treatmentPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateTreatmentPlanCommandHandler(
        ITreatmentPlanRepository treatmentPlanRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _treatmentPlanRepository = treatmentPlanRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateTreatmentPlanCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException($"Vet request {request.VetRequestId} not found.");

        await _authorization.EnsureIncidentAccessAsync(vetRequest.IncidentId, ct);

        var plan = new TreatmentPlan(request.VetRequestId, request.AuthoredByVetId, request.InstructionsText, request.Language);
        await _treatmentPlanRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return plan.Id;
    }
}

public class CreateTreatmentPlanCommandValidator : AbstractValidator<CreateTreatmentPlanCommand>
{
    public CreateTreatmentPlanCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.AuthoredByVetId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.InstructionsText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Language).MaximumLength(10);
    }
}
