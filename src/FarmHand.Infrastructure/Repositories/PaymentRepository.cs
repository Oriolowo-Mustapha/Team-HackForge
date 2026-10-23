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

    private sealed class Row
    {
        public required Payment P { get; init; }
        public required VetRequest V { get; init; }
        public required Guid IncidentId { get; init; }
        public required Farm F { get; init; }
    }

    public async Task<PagedList<PaymentRow>> SearchAsync(PaymentSearch search, CancellationToken ct = default)
    {
        var query =
            from p in DbSet
            join v in DbContext.Set<VetRequest>() on p.VetRequestId equals v.Id
            join i in DbContext.Set<Incident>() on v.IncidentId equals i.Id
            join f in DbContext.Set<Farm>() on i.FarmId equals f.Id
            select new Row { P = p, V = v, IncidentId = i.Id, F = f };

        if (search.FarmIds is not null) query = query.Where(r => search.FarmIds.Contains(r.F.Id));
        if (search.VetPartnerId is { } vetId) query = query.Where(r => r.V.VetPartnerId == vetId);
        if (search.VetRequestId is { } requestId) query = query.Where(r => r.P.VetRequestId == requestId);
        if (!string.IsNullOrWhiteSpace(search.Status)) query = query.Where(r => r.P.Status == search.Status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.P.CreatedAt)
            .Skip(search.Paging.Skip)
            .Take(search.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(ct);
        return new PagedList<PaymentRow>(items.Select(r => new PaymentRow(r.P, r.V, r.IncidentId, r.F)).ToList(), total);
    }

    public async Task<Payment?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(p => p.ProviderTransactionId == providerTransactionId, ct);
    }

    public async Task<IReadOnlyList<Payment>> GetByVetRequestIdsAsync(IEnumerable<Guid> vetRequestIds, CancellationToken ct = default)
    {
        var ids = vetRequestIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(p => ids.Contains(p.VetRequestId)).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }
}
