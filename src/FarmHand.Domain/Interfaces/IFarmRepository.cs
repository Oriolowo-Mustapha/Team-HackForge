namespace FarmHand.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<Farm>> ListByOwnerIdAsync(string ownerId, CancellationToken ct = default);
    Task<PagedList<Farm>> ListAllAsync(string? text, Paging paging, CancellationToken ct = default);
}
