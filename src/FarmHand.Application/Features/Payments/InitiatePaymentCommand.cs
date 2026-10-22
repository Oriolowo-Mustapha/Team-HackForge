using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.ValueObjects;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Creates a pending payment and obtains a provider reference.</summary>
/// <param name="VetRequestId">Vet request being paid for; must be accessible to the caller.</param>
/// <param name="Amount">Amount to charge; must be greater than zero.</param>
/// <param name="Currency">ISO currency code, for example "NGN".</param>
public record InitiatePaymentCommand(Guid VetRequestId, decimal Amount, string Currency) : IRequest<string>;

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, string>
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public InitiatePaymentCommandHandler(
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<string> Handle(InitiatePaymentCommand request, CancellationToken ct)
    {
        await _authorization.EnsureVetRequestAccessAsync(request.VetRequestId, ct);

        var currency = request.Currency.Trim().ToUpperInvariant();
        var result = await _paymentService.InitiatePaymentAsync(request.VetRequestId, request.Amount, currency, ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.TransactionReference))
            throw new InvalidOperationException(result.ErrorMessage ?? "Payment initiation failed.");

        var payment = new Payment(request.VetRequestId, new Money(request.Amount, currency));
        payment.MarkPending(result.TransactionReference);

        await _paymentRepository.AddAsync(payment, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return result.TransactionReference;
    }
}

public class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3).MaximumLength(10);
    }
}
