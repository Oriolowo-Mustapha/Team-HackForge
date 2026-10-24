namespace FarmHand.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<Farm>> ListByOwnerIdAsync(string ownerId, CancellationToken ct = default);
    Task<PagedList<Farm>> ListAllAsync(string? text, Paging paging, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, FarmRecordCounts>> GetRecordCountsAsync(IReadOnlyCollection<Guid> farmIds, DateTime since, CancellationToken ct = default);
}

public record FarmRecordCounts(int Groups, int Animals, int Headcount, int OpenIncidents, int PendingObservations, int Incidents, int RecentlyResolved);
