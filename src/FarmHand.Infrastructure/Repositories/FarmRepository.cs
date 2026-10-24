using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class FarmRepository : EfRepository<Farm>, IFarmRepository
{
    public FarmRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<Farm?> GetByOwnerIdAsync(string ownerId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(f => f.OwnerId == ownerId, ct);
    }

    public async Task<IReadOnlyList<Farm>> ListByOwnerIdAsync(string ownerId, CancellationToken ct = default)
    {
        return await DbSet.Where(f => f.OwnerId == ownerId).OrderBy(f => f.CreatedAt).ToListAsync(ct);
    }

    public async Task<PagedList<Farm>> ListAllAsync(string? text, Paging paging, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{text.Trim()}%";
            query = query.Where(f => EF.Functions.ILike(f.DisplayName, pattern) || EF.Functions.ILike(f.Location, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id).Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);
        return new PagedList<Farm>(items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, FarmRecordCounts>> GetRecordCountsAsync(IReadOnlyCollection<Guid> farmIds, DateTime since, CancellationToken ct = default)
    {
        // Correlated aggregates run in PostgreSQL in one round trip for all owned farms.
        return await DbSet.AsNoTracking().Where(f => farmIds.Contains(f.Id)).Select(f => new
        {
            f.Id,
            Groups = DbContext.LivestockCategories.Count(c => c.FarmId == f.Id),
            Animals = DbContext.LivestockAnimals.Count(a => a.FarmId == f.Id && a.IsActive),
            Headcount = DbContext.LivestockCategories.Where(c => c.FarmId == f.Id).Sum(c => (int?)c.AnimalCount) ?? 0,
            Open = DbContext.Incidents.Count(i => i.FarmId == f.Id && i.Status != Domain.Enums.IncidentStatus.Resolved),
            Pending = DbContext.PoultryObservations.Count(o => o.FarmId == f.Id && o.ConfirmationStatus == Domain.Enums.ObservationConfirmationStatus.Pending),
            Incidents = DbContext.Incidents.Count(i => i.FarmId == f.Id),
            Resolved = DbContext.Incidents.Count(i => i.FarmId == f.Id && i.ResolvedAt != null && i.ResolvedAt >= since)
        }).ToDictionaryAsync(x => x.Id, x => new FarmRecordCounts(x.Groups, x.Animals, x.Headcount, x.Open, x.Pending, x.Incidents, x.Resolved), ct);
    }
}
