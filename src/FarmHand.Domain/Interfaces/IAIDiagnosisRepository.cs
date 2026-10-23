namespace FarmHand.Domain.Interfaces;

public interface IAIDiagnosisRepository : IRepository<AIDiagnosis>
{
    Task<AIDiagnosis?> GetByObservationIdAsync(Guid observationId, CancellationToken ct = default);
    Task<IReadOnlyList<AIDiagnosis>> GetByObservationIdsAsync(IEnumerable<Guid> observationIds, CancellationToken ct = default);
}
