using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class AIDiagnosisRepository : EfRepository<AIDiagnosis>, IAIDiagnosisRepository
{
    public AIDiagnosisRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<AIDiagnosis?> GetByObservationIdAsync(Guid observationId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(d => d.ObservationId == observationId, ct);
    }

    public async Task<IReadOnlyList<AIDiagnosis>> GetByObservationIdsAsync(IEnumerable<Guid> observationIds, CancellationToken ct = default)
    {
        var ids = observationIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(d => ids.Contains(d.ObservationId)).ToListAsync(ct);
    }
}
