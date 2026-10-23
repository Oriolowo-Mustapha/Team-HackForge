using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Lists the cost-share contributions recorded for a vet request.</summary>
/// <param name="VetRequestId">Vet request id owned by the caller.</param>
public record GetCostSharesQuery(Guid VetRequestId) : IRequest<CostShareListDto>;

/// <summary>
/// Cost-share ledger for one vet request: individual contributions plus their running total,
/// so the coordinating farmer or treasurer can see how much of the farmer cost has been covered.
/// </summary>
/// <param name="VetRequestId">Vet request the contributions belong to.</param>
/// <param name="Items">Individual contributions with payer type and amount.</param>
/// <param name="TotalAmount">Sum of all contributions, in the request currency.</param>
/// <param name="TargetAmount">The vet request's farmer cost.</param>
public record CostShareListDto(
    Guid VetRequestId,
    IReadOnlyList<CostShareItemDto> Items,
    decimal TotalAmount,
    decimal TargetAmount);

/// <summary>One recorded cost-share contribution.</summary>
/// <param name="Id">Cost share record id.</param>
/// <param name="PayerType">Payer category, for example "coop" or "ngo".</param>
/// <param name="Amount">Contributed amount in NGN.</param>
/// <param name="CommittedAt">When the contribution was recorded.</param>
public record CostShareItemDto(Guid Id, string PayerType, decimal Amount, DateTime CommittedAt);

public class GetCostSharesQueryHandler : IRequestHandler<GetCostSharesQuery, CostShareListDto>
{
    private readonly ICostShareRepository _costShareRepository;
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetCostSharesQueryHandler(
        ICostShareRepository costShareRepository,
        IVetRequestRepository vetRequestRepository,
        IResourceAuthorizationService authorization)
    {
        _costShareRepository = costShareRepository;
        _vetRequestRepository = vetRequestRepository;
        _authorization = authorization;
    }

    public async Task<CostShareListDto> Handle(GetCostSharesQuery request, CancellationToken ct)
    {
        await _authorization.EnsureVetRequestAccessAsync(request.VetRequestId, ct);

        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException("Vet request not found.");

        var shares = (await _costShareRepository.GetByVetRequestIdAsync(request.VetRequestId, ct))
            .OrderBy(s => s.CreatedAt)
            .ToList();

        var items = shares
            .Select(s => new CostShareItemDto(s.Id, s.PayerType, s.Amount.Amount, s.CommittedAt))
            .ToList();

        var total = shares.Sum(s => s.Amount.Amount);

        return new CostShareListDto(request.VetRequestId, items, total, vetRequest.FarmerCost.Amount);
    }
}
