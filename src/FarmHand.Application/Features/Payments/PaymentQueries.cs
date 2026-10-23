using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Pre-payment summary shown before the farmer commits to a checkout.</summary>
public record PaymentQuoteDto(
    Guid VetRequestId,
    decimal VetFee,
    decimal CommissionPercentage,
    decimal PlatformCommission,
    decimal Total,
    string Currency,
    string Mode,
    string? VetName,
    bool VetPayoutSplit,
    bool CanPay,
    string? Reason,
    PaymentDto? PendingPayment);

public record GetPaymentQuoteQuery(Guid VetRequestId) : IRequest<PaymentQuoteDto>;

public class GetPaymentQuoteQueryHandler : IRequestHandler<GetPaymentQuoteQuery, PaymentQuoteDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IPaymentService _paymentService;

    public GetPaymentQuoteQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IPaymentService paymentService)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _paymentService = paymentService;
    }

    public async Task<PaymentQuoteDto> Handle(GetPaymentQuoteQuery request, CancellationToken ct)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(request.VetRequestId, ct)
            ?? throw PublicException.NotFound("Vet request not found.");
        var incident = await _unitOfWork.Incidents.GetByIdAsync(vetRequest.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");
        await _authorization.EnsureFarmAccessAsync(incident.FarmId, ct);

        var vet = vetRequest.VetPartnerId is { } vetId ? await _unitOfWork.VetPartners.GetByIdAsync(vetId, ct) : null;
        var payments = (await _unitOfWork.Payments.GetByVetRequestIdAsync(vetRequest.Id, ct)).ToList();
        var pending = payments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);

        var fee = vetRequest.FarmerCost.Amount;
        var commission = PaymentPolicy.Commission(fee);
        string? reason = null;
        if (fee <= 0) reason = "This request does not require payment.";
        else if (payments.Any(p => p.Status == PaymentStatus.Completed)) reason = "This request has already been paid.";
        else if (vetRequest.Status == RequestStatus.Pending) reason = "Payment opens once a vet accepts your request.";
        else if (vetRequest.Status != RequestStatus.Responded) reason = "This request is closed.";
        else if (!_paymentService.IsEnabled) reason = "Payments are not configured on this server.";

        return new PaymentQuoteDto(
            vetRequest.Id, fee, PaymentPolicy.PlatformCommissionPercentage, commission, fee + commission,
            vetRequest.FarmerCost.Currency, _paymentService.Mode, vet?.Name,
            vet is { HasPayoutAccount: true }, reason is null, reason,
            pending?.ToDto(includePayout: false));
    }
}

/// <summary>Payment history. Farmers see their farms' payments, vets see payments for their assigned work (with payout amounts), admins see all.</summary>
public record GetPaymentsQuery(Guid? VetRequestId, string? Status, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PaymentDto>>;

public class GetPaymentsQueryHandler : IRequestHandler<GetPaymentsQuery, PagedResult<PaymentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public GetPaymentsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken ct)
    {
        var paging = new Paging(request.Page, request.PageSize);
        PaymentSearch search;
        var includePayout = _currentUser.IsAdmin;

        if (_currentUser.IsVet)
        {
            var vet = await _authorization.GetCurrentVetAsync(ct)
                ?? throw PublicException.Forbidden("No active vet profile is linked to this account.");
            search = new PaymentSearch(null, vet.Id, request.VetRequestId, request.Status, paging);
            includePayout = true;
        }
        else
        {
            search = new PaymentSearch(await _authorization.GetAccessibleFarmIdsAsync(ct), null, request.VetRequestId, request.Status, paging);
        }

        var page = await _unitOfWork.Payments.SearchAsync(search, ct);
        var items = page.Items.Select(r => r.Payment.ToDto(includePayout, r.Farm.DisplayName, r.IncidentId)).ToList();
        return new PagedResult<PaymentDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}

public record GetPaymentQuery(string TransactionReference) : IRequest<PaymentDto>;

public class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public GetPaymentQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<PaymentDto> Handle(GetPaymentQuery request, CancellationToken ct)
    {
        var payment = await _unitOfWork.Payments.GetByTransactionReferenceAsync(request.TransactionReference.Trim(), ct)
            ?? throw PublicException.NotFound("Unknown payment reference.");
        await VerifyPaymentCommandHandler.EnsureOwnerAsync(payment, _unitOfWork, _authorization, ct);
        return payment.ToDto(includePayout: false);
    }
}
