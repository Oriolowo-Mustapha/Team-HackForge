using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class VetRequestRepository : EfRepository<VetRequest>, IVetRequestRepository
{
    public VetRequestRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<VetRequest>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.Where(v => v.IncidentId == incidentId).ToListAsync(ct);
    }

    public async Task<IEnumerable<VetRequest>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet
            .Join(
                DbContext.Set<Incident>(),
                v => v.IncidentId,
                i => i.Id,
                (v, i) => new { v, i })
            .Where(x => x.i.FarmId == farmId)
            .Select(x => x.v)
            .ToListAsync(ct);
    }

    public async Task<VetRequest?> GetByIdWithDetailsAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(v => v.Id == vetRequestId, ct);
    }

    private sealed class Row
    {
        public required VetRequest V { get; init; }
        public required Incident I { get; init; }
        public required Farm F { get; init; }
    }

    private IQueryable<Row> Rows() =>
        from v in DbSet
        join i in DbContext.Set<Incident>() on v.IncidentId equals i.Id
        join f in DbContext.Set<Farm>() on i.FarmId equals f.Id
        select new Row { V = v, I = i, F = f };

    public async Task<IReadOnlyList<VetRequestRow>> GetActiveByFarmAsync(Guid farmId, CancellationToken ct = default)
    {
        var rows = await Rows().AsNoTracking().Where(r => r.F.Id == farmId
            && (r.V.Status == Domain.Enums.RequestStatus.Pending || r.V.Status == Domain.Enums.RequestStatus.Responded))
            .OrderByDescending(r => r.V.CreatedAt).ThenBy(r => r.V.Id).ToListAsync(ct);
        return rows.Select(r => new VetRequestRow(r.V, r.I, r.F)).ToList();
    }

    public async Task<PagedList<VetRequestRow>> SearchAsync(VetRequestSearch search, CancellationToken ct = default)
    {
        var query = Rows();
        if (search.FarmIds is not null) query = query.Where(r => search.FarmIds.Contains(r.F.Id));
        if (search.AssignedVetPartnerId is { } assigned)
            query = query.Where(r => r.V.VetPartnerId == assigned && r.V.Status != Domain.Enums.RequestStatus.Pending);
        if (search.OpenForVetPartnerId is { } openFor)
            query = query.Where(r => r.V.Status == Domain.Enums.RequestStatus.Pending
                && (r.V.VetPartnerId == null || r.V.VetPartnerId == openFor));
        if (search.Status is { } status) query = query.Where(r => r.V.Status == status);
        if (search.IncidentId is { } incidentId) query = query.Where(r => r.V.IncidentId == incidentId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.V.CreatedAt)
            .ThenBy(r => r.V.Id)
            .Skip(search.Paging.Skip)
            .Take(search.Paging.PageSize)
            .AsNoTracking()
            .ToListAsync(ct);
        return new PagedList<VetRequestRow>(items.Select(r => new VetRequestRow(r.V, r.I, r.F)).ToList(), total);
    }

    public async Task<VetRequestRow?> GetRowAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        var row = await Rows().AsNoTracking().FirstOrDefaultAsync(r => r.V.Id == vetRequestId, ct);
        return row is null ? null : new VetRequestRow(row.V, row.I, row.F);
    }

    public async Task<IReadOnlyList<VetRequest>> GetByIncidentIdsAsync(IEnumerable<Guid> incidentIds, CancellationToken ct = default)
    {
        var ids = incidentIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(v => ids.Contains(v.IncidentId)).OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
    }

    public async Task<bool> TryClaimAsync(Guid vetRequestId, Guid vetPartnerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var updated = await DbSet
            .Where(v => v.Id == vetRequestId
                && v.Status == Domain.Enums.RequestStatus.Pending
                && (v.VetPartnerId == null || v.VetPartnerId == vetPartnerId))
            .ExecuteUpdateAsync(set => set
                .SetProperty(v => v.Status, Domain.Enums.RequestStatus.Responded)
                .SetProperty(v => v.VetPartnerId, vetPartnerId)
                .SetProperty(v => v.RespondedAt, now)
                .SetProperty(v => v.UpdatedAt, now), ct);
        return updated == 1;
    }
}
