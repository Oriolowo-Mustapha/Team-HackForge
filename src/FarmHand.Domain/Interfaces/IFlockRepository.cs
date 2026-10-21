namespace FarmHand.Domain.Interfaces;

public interface IFlockRepository : IRepository<Flock>
{
    Task<IEnumerable<Flock>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
}
