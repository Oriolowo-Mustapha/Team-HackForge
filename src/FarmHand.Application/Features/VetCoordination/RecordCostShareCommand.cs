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

    public RecordCostShareCommandHandler(
        ICostShareRepository costShareRepository,
        IUnitOfWork unitOfWork)
    {
        _costShareRepository = costShareRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RecordCostShareCommand request, CancellationToken ct)
    {
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
