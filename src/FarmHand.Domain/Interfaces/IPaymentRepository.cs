namespace FarmHand.Domain.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IEnumerable<Payment>> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default);
    Task<Payment?> GetByTransactionReferenceAsync(string transactionReference, CancellationToken ct = default);
    Task<PagedList<PaymentRow>> SearchAsync(PaymentSearch search, CancellationToken ct = default);
    Task<Payment?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByVetRequestIdsAsync(IEnumerable<Guid> vetRequestIds, CancellationToken ct = default);
}
