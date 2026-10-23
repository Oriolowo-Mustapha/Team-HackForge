namespace FarmHand.Application.Common.Interfaces;

/// <summary>
/// Records audit events for security- and money-relevant actions. Events are added to the current
/// unit of work and persisted with the caller's next SaveChanges.
/// </summary>
public interface IAuditLogger
{
    Task RecordAsync(string eventType, Guid? relatedRecordId = null, string? details = null, string? actorIdOverride = null, CancellationToken ct = default);
}
