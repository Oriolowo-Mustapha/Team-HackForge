namespace FarmHand.Domain.Interfaces;

public interface IPoultryObservationRepository : IRepository<PoultryObservation>
{
    Task<IEnumerable<PoultryObservation>> GetByFlockIdAsync(Guid flockId, CancellationToken ct = default);
    Task<IEnumerable<PoultryObservation>> GetRecentByFlockIdAsync(Guid flockId, int days, CancellationToken ct = default);
    Task<bool> ExistsUnconfirmedAsync(Guid flockId, DateTime recordedAt, CancellationToken ct = default);
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<PoultryObservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
}
