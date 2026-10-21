namespace FarmHand.Domain.Interfaces;

public interface ICostShareRepository : IRepository<CostShare>
{
    Task<IEnumerable<CostShare>> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default);
}
