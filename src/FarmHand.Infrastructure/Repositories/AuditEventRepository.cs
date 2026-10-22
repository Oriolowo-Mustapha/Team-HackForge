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
}
