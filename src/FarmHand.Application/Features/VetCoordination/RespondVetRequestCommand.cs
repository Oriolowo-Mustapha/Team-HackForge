using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>
/// A vet accepts a pending request. The claim is a single atomic database update, so when two vets
/// accept an open request at the same time exactly one succeeds and the other receives 409.
/// Administrators may assign a vet by passing <paramref name="VetPartnerId"/>.
/// </summary>
public record RespondVetRequestCommand(Guid VetRequestId, Guid? VetPartnerId = null) : IRequest;

public class RespondVetRequestCommandHandler : IRequestHandler<RespondVetRequestCommand>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public RespondVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser,
        IAuditLogger audit)
    {
        _vetRequestRepository = vetRequestRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Unit> Handle(RespondVetRequestCommand request, CancellationToken ct)
    {
        _authorization.EnsureVetOrAdmin();

        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");

        Guid vetPartnerId;
        if (_currentUser.IsAdmin && !_currentUser.IsVet)
        {
            vetPartnerId = request.VetPartnerId ?? vetRequest.VetPartnerId
                ?? throw PublicException.BadRequest("Choose the vet to assign.");
            var vet = await _unitOfWork.VetPartners.GetByIdAsync(vetPartnerId, ct);
            if (vet is not { IsActive: true })
                throw PublicException.BadRequest("The selected vet is not available.");
        }
        else
        {
            var partner = await _authorization.GetCurrentVetAsync(ct)
                ?? throw PublicException.Forbidden("No active vet profile is linked to this account.");

            if (request.VetPartnerId is { } requestedId && requestedId != partner.Id)
                throw PublicException.Forbidden("You can only respond as your own vet profile.");

            vetPartnerId = partner.Id;
        }

        if (vetRequest.Status != RequestStatus.Pending)
            throw PublicException.Conflict(vetRequest.VetPartnerId == vetPartnerId && vetRequest.Status == RequestStatus.Responded
                ? "You have already accepted this request."
                : "This request is no longer open. Another vet may have accepted it.");

        if (vetRequest.VetPartnerId is { } targeted && targeted != vetPartnerId)
            throw PublicException.Forbidden("This request was sent to another vet.");

        var claimed = await _vetRequestRepository.TryClaimAsync(vetRequest.Id, vetPartnerId, ct);
        if (!claimed)
            throw PublicException.Conflict("Another vet accepted this request first.");

        await _audit.RecordAsync("vet_request.accepted", vetRequest.Id, $"vet={vetPartnerId}", ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class RespondVetRequestCommandValidator : AbstractValidator<RespondVetRequestCommand>
{
    public RespondVetRequestCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
    }
}

/// <summary>The vet a request was sent to declines it. Open (untargeted) requests are simply left for others.</summary>
public record DeclineVetRequestCommand(Guid VetRequestId, string? Reason) : IRequest;

public class DeclineVetRequestCommandHandler : IRequestHandler<DeclineVetRequestCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public DeclineVetRequestCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<Unit> Handle(DeclineVetRequestCommand request, CancellationToken ct)
    {
        var vet = await _authorization.GetCurrentVetAsync(ct)
            ?? throw PublicException.Forbidden("Only the requested vet can decline.");
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");

        if (vetRequest.VetPartnerId != vet.Id)
            throw PublicException.Forbidden("Only the vet this request was sent to can decline it.");
        if (vetRequest.Status != RequestStatus.Pending)
            throw PublicException.Conflict("Only a pending request can be declined.");

        vetRequest.Decline(string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());
        await _audit.RecordAsync("vet_request.declined", vetRequest.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class DeclineVetRequestCommandValidator : AbstractValidator<DeclineVetRequestCommand>
{
    public DeclineVetRequestCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>The farmer (or an admin) withdraws a request that has not been paid for.</summary>
public record CancelVetRequestCommand(Guid VetRequestId) : IRequest;

public class CancelVetRequestCommandHandler : IRequestHandler<CancelVetRequestCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public CancelVetRequestCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<Unit> Handle(CancelVetRequestCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        var incident = await _unitOfWork.Incidents.GetByIdAsync(vetRequest.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");
        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        if (!vetRequest.IsActive)
            throw PublicException.Conflict("This request is already closed.");

        var payments = await _unitOfWork.Payments.GetByVetRequestIdAsync(vetRequest.Id, ct);
        if (payments.Any(p => p.Status == PaymentStatus.Completed))
            throw PublicException.Conflict("This request has been paid. Contact support to arrange a cancellation and refund.");
        if (payments.Any(p => p.Status == PaymentStatus.Pending && !p.IsSimulated))
            throw PublicException.Conflict("Verify or cancel the pending live checkout before cancelling this request. A provider confirmation may still be on its way.");

        foreach (var pending in payments.Where(p => p.Status == PaymentStatus.Pending))
            pending.Cancel();

        vetRequest.Cancel();
        await _audit.RecordAsync("vet_request.cancelled", vetRequest.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>
/// The assigned vet (or an admin) closes the request after delivering the service. Paid services
/// can only be completed once the payment is verified.
/// </summary>
public record CompleteVetRequestCommand(Guid VetRequestId, string? Notes) : IRequest;

public class CompleteVetRequestCommandHandler : IRequestHandler<CompleteVetRequestCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;

    public CompleteVetRequestCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ICurrentUserService currentUser, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<Unit> Handle(CompleteVetRequestCommand request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");

        if (!_currentUser.IsAdmin)
        {
            var vet = await _authorization.GetCurrentVetAsync(ct);
            if (vet is null || vetRequest.VetPartnerId != vet.Id)
                throw PublicException.Forbidden("Only the assigned vet can complete this request.");
        }

        if (vetRequest.Status != RequestStatus.Responded)
            throw PublicException.Conflict("Only an accepted request can be completed.");

        if (vetRequest.FarmerCost.Amount > 0)
        {
            var payments = await _unitOfWork.Payments.GetByVetRequestIdAsync(vetRequest.Id, ct);
            if (!payments.Any(p => p.Status == PaymentStatus.Completed))
                throw PublicException.Conflict("The farmer's payment has not been verified yet.");
        }

        vetRequest.Complete(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());
        await _audit.RecordAsync("vet_request.completed", vetRequest.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class CompleteVetRequestCommandValidator : AbstractValidator<CompleteVetRequestCommand>
{
    public CompleteVetRequestCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>
/// Lists vet requests for the caller. Scopes: farmers see their farms' requests; vets use
/// <c>open</c> (pending requests they may accept) or <c>assigned</c> (their accepted and finished work);
/// administrators see everything.
/// </summary>
public record GetVetRequestsQuery(string? Scope, RequestStatus? Status, Guid? IncidentId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<VetRequestDetailDto>>;

public class GetVetRequestsQueryHandler : IRequestHandler<GetVetRequestsQuery, PagedResult<VetRequestDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;
    private readonly VetRequestProjector _projector;

    public GetVetRequestsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ICurrentUserService currentUser, VetRequestProjector projector)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
        _projector = projector;
    }

    public async Task<PagedResult<VetRequestDetailDto>> Handle(GetVetRequestsQuery request, CancellationToken ct)
    {
        var paging = new Paging(request.Page, request.PageSize);
        VetRequestSearch search;

        if (_currentUser.IsVet)
        {
            var vet = await _authorization.GetCurrentVetAsync(ct)
                ?? throw PublicException.Forbidden("No active vet profile is linked to this account.");
            search = string.Equals(request.Scope, "open", StringComparison.OrdinalIgnoreCase)
                ? new VetRequestSearch(null, null, vet.Id, null, request.IncidentId, paging)
                : new VetRequestSearch(null, vet.Id, null, request.Status, request.IncidentId, paging);
        }
        else
        {
            var farmIds = await _authorization.GetAccessibleFarmIdsAsync(ct);
            search = new VetRequestSearch(farmIds, null, null, request.Status, request.IncidentId, paging);
        }

        var page = await _unitOfWork.VetRequests.SearchAsync(search, ct);
        var items = await _projector.ProjectAsync(page.Items, ct);
        return new PagedResult<VetRequestDetailDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}

public record GetVetRequestQuery(Guid VetRequestId) : IRequest<VetRequestDetailDto>;

public class GetVetRequestQueryHandler : IRequestHandler<GetVetRequestQuery, VetRequestDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly VetRequestProjector _projector;

    public GetVetRequestQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, VetRequestProjector projector)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _projector = projector;
    }

    public async Task<VetRequestDetailDto> Handle(GetVetRequestQuery request, CancellationToken ct)
    {
        await _authorization.EnsureVetRequestAccessAsync(request.VetRequestId, ct);
        var row = await _unitOfWork.VetRequests.GetRowAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        return (await _projector.ProjectAsync([row], ct))[0];
    }
}
