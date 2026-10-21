namespace FarmHand.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default);
}
