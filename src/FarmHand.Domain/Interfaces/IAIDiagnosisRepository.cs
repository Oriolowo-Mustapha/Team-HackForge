namespace FarmHand.Domain.Interfaces;

public interface IAIDiagnosisRepository : IRepository<AIDiagnosis>
{
    Task<AIDiagnosis?> GetByObservationIdAsync(Guid observationId, CancellationToken ct = default);
}
