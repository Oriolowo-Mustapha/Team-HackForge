using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class PaymentRepository : EfRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<Payment>> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        return await DbSet.Where(p => p.VetRequestId == vetRequestId).ToListAsync(ct);
    }

    public async Task<Payment?> GetByTransactionReferenceAsync(string transactionReference, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(p => p.TransactionReference == transactionReference, ct);
    }
}
