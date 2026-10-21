namespace FarmHand.Domain.Interfaces;

public interface IPoultryObservationRepository : IRepository<PoultryObservation>
{
    Task<IEnumerable<PoultryObservation>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default);
    Task<IEnumerable<PoultryObservation>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
    Task<IEnumerable<PoultryObservation>> GetRecentByAnimalIdAsync(Guid animalId, int days, CancellationToken ct = default);
    Task<bool> ExistsUnconfirmedAsync(Guid animalId, DateTime recordedAt, CancellationToken ct = default);
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<PoultryObservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
}
