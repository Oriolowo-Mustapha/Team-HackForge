namespace FarmHand.Domain.Interfaces;

public interface ILivestockRecordRepository : IRepository<LivestockRecord>
{
    Task<IEnumerable<LivestockRecord>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
}
