namespace FarmHand.Domain.Interfaces;

public interface IAuditEventRepository : IRepository<AuditEvent>
{
    Task<IEnumerable<AuditEvent>> GetByActorIdAsync(string actorId, CancellationToken ct = default);
    Task<IEnumerable<AuditEvent>> GetByEventTypeAsync(string eventType, CancellationToken ct = default);
}
