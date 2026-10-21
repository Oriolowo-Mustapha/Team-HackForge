using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Payments;

/// <summary>Records a completed payment. No provider verification is performed.</summary>
/// <param name="VetRequestId">Vet request being paid for.</param>
/// <param name="Amount">Amount in NGN; must be greater than zero.</param>
/// <param name="TransactionReference">Provider or client reference. Not unique, not validated.</param>
public record RecordPaymentCommand(
    Guid VetRequestId,
    decimal Amount,
    string TransactionReference) : IRequest;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordPaymentCommandHandler(
        IPaymentRepository paymentRepository,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var payment = new Payment(
            request.VetRequestId,
            new Domain.ValueObjects.Money(request.Amount));

        payment.MarkCompleted(request.TransactionReference);

        await _paymentRepository.AddAsync(payment, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionReference).NotEmpty();
    }
}
