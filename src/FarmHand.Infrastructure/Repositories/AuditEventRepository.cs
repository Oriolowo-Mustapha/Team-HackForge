using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class AuditEventRepository : EfRepository<AuditEvent>, IAuditEventRepository
{
    public AuditEventRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<AuditEvent>> GetByActorIdAsync(string actorId, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.ActorId == actorId).OrderByDescending(a => a.Timestamp).ToListAsync(ct);
    }

    public async Task<IEnumerable<AuditEvent>> GetByEventTypeAsync(string eventType, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.EventType == eventType).OrderByDescending(a => a.Timestamp).ToListAsync(ct);
    }

    public async Task<PagedList<AuditEvent>> SearchAsync(AuditEventSearch search, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.ActorId)) query = query.Where(a => a.ActorId == search.ActorId);
        if (!string.IsNullOrWhiteSpace(search.EventType)) query = query.Where(a => a.EventType == search.EventType);
        if (search.RelatedRecordId is { } related) query = query.Where(a => a.RelatedRecordId == related);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.Timestamp).Skip(search.Paging.Skip).Take(search.Paging.PageSize).ToListAsync(ct);
        return new PagedList<AuditEvent>(items, total);
    }
}
