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
}
