namespace FarmHand.Domain.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default);
}
