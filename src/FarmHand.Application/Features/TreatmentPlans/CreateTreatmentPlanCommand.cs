using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.TreatmentPlans;

/// <summary>Create a treatment plan for a vet request.</summary>
/// <param name="VetRequestId">Vet request id; must be accessible to the caller.</param>
/// <param name="AuthoredByVetId">Vet id authoring the plan; vets cannot override their own profile id.</param>
/// <param name="InstructionsText">Care instructions.</param>
/// <param name="Language">Language of the instructions.</param>
public record CreateTreatmentPlanCommand(Guid VetRequestId, string AuthoredByVetId, string InstructionsText, string? Language) : IRequest<Guid>;

public class CreateTreatmentPlanCommandHandler : IRequestHandler<CreateTreatmentPlanCommand, Guid>
{
    private readonly ITreatmentPlanRepository _treatmentPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public CreateTreatmentPlanCommandHandler(
        ITreatmentPlanRepository treatmentPlanRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser)
    {
        _treatmentPlanRepository = treatmentPlanRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateTreatmentPlanCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException($"Vet request {request.VetRequestId} not found.");

        await _authorization.EnsureIncidentAccessAsync(vetRequest.IncidentId, ct);

        var authoredByVetId = request.AuthoredByVetId?.Trim() ?? string.Empty;

        if (_currentUser.IsAdmin && _currentUser.UserId is not { } adminUserId)
            throw new UnauthorizedAccessException("An authenticated administrator is required to author this plan.");

        var vetPartner = _currentUser.UserId is { } userId
            ? await _unitOfWork.VetPartners.GetByUserIdAsync(userId, ct)
            : null;

        if (vetPartner is not null)
        {
            if (vetRequest.VetPartnerId is { } assignedVetId && assignedVetId != vetPartner.Id)
                throw new UnauthorizedAccessException("This request is assigned to another vet.");

            if (!_currentUser.IsAdmin)
                authoredByVetId = vetPartner.Id.ToString();
            else if (!Guid.TryParse(authoredByVetId, out var suppliedAdminAuthorId) || suppliedAdminAuthorId != vetPartner.Id)
                authoredByVetId = vetPartner.Id.ToString();
        }

        var plan = new TreatmentPlan(request.VetRequestId, authoredByVetId, request.InstructionsText, request.Language);
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
