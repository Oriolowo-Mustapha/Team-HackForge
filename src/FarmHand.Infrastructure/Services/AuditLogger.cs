namespace FarmHand.Infrastructure.Services;

public class AuditLogger : IAuditLogger
{
    private readonly IAuditEventRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public AuditLogger(IAuditEventRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task RecordAsync(string eventType, Guid? relatedRecordId = null, string? details = null, string? actorIdOverride = null, CancellationToken ct = default)
    {
        var actor = actorIdOverride ?? _currentUser.UserId?.ToString() ?? "system";
        var safeDetails = details is { Length: > 1000 } ? details[..1000] : details;
        await _repository.AddAsync(new AuditEvent(actor, eventType, relatedRecordId, safeDetails), ct);
    }
}
