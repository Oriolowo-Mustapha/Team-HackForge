using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Records a cost-share contribution for a vet request.</summary>
/// <param name="VetRequestId">Vet request id; must match the route value.</param>
/// <param name="PayerType">Free-text payer category, for example "farmer", "coop", or "insurer".</param>
/// <param name="Amount">Amount in NGN; must be zero or greater.</param>
public record RecordCostShareCommand(
    Guid VetRequestId,
    string PayerType,
    decimal Amount) : IRequest;

public class RecordCostShareCommandHandler : IRequestHandler<RecordCostShareCommand>
{
    private readonly ICostShareRepository _costShareRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public RecordCostShareCommandHandler(
        ICostShareRepository costShareRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _costShareRepository = costShareRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Unit> Handle(RecordCostShareCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException("Vet request not found.");
        var access = await _authorization.GetIncidentAccessAsync(vetRequest.IncidentId, ct);
        if (access is not (IncidentAccess.Owner or IncidentAccess.Admin))
            throw new UnauthorizedAccessException("Only the farm owner can record cost-share contributions.");

        var costShare = new CostShare(
            request.VetRequestId,
            request.PayerType,
            new Domain.ValueObjects.Money(request.Amount));

        await _costShareRepository.AddAsync(costShare, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class RecordCostShareCommandValidator : AbstractValidator<RecordCostShareCommand>
{
    public RecordCostShareCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.PayerType).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
    }
}
