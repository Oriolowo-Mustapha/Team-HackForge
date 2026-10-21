using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class PoultryObservationRepository : EfRepository<PoultryObservation>, IPoultryObservationRepository
{
    public PoultryObservationRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<PoultryObservation>> GetByFlockIdAsync(Guid flockId, CancellationToken ct = default)
    {
        return await DbSet.Where(o => o.FlockId == flockId).OrderByDescending(o => o.RecordedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<PoultryObservation>> GetRecentByFlockIdAsync(Guid flockId, int days, CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);
        return await DbSet.Where(o => o.FlockId == flockId && o.RecordedAt >= fromDate).ToListAsync(ct);
    }

    public async Task<bool> ExistsUnconfirmedAsync(Guid flockId, DateTime recordedAt, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(o => o.FlockId == flockId && o.RecordedAt.Date == recordedAt.Date && o.ConfirmationStatus != Domain.Enums.ObservationConfirmationStatus.Confirmed, ct);
    }
}
