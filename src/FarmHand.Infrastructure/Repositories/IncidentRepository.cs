using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class IncidentRepository : EfRepository<Incident>, IIncidentRepository
{
    public IncidentRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<Incident>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(i => i.FarmId == farmId).OrderByDescending(i => i.DetectedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<Incident>> GetOpenByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(i => i.FarmId == farmId && i.Status != Domain.Enums.IncidentStatus.Resolved).ToListAsync(ct);
    }

    public async Task<Incident?> GetByIdWithActionsAsync(Guid incidentId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(i => i.Id == incidentId, ct);
    }

    public async Task<PagedList<Incident>> SearchAsync(IncidentSearch search, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (search.FarmIds is not null) query = query.Where(i => search.FarmIds.Contains(i.FarmId));
        if (search.IncidentIds is not null) query = query.Where(i => search.IncidentIds.Contains(i.Id));
        if (search.AssignedVetPartnerId is { } assignedVet)
            query = query.Where(i => DbContext.VetRequests.Any(v => v.IncidentId == i.Id
                && v.VetPartnerId == assignedVet
                && (v.Status == Domain.Enums.RequestStatus.Responded || v.Status == Domain.Enums.RequestStatus.Completed)));
        if (search.Status is { } status) query = query.Where(i => i.Status == status);
        if (search.OpenOnly) query = query.Where(i => i.Status != Domain.Enums.IncidentStatus.Resolved);
        if (search.Severity is { } severity) query = query.Where(i => i.Severity == severity);
        if (search.AnimalId is { } animalId) query = query.Where(i => i.LivestockAnimalId == animalId);
        if (search.CategoryId is { } categoryId) query = query.Where(i => i.LivestockCategoryId == categoryId);
        if (search.From is { } from) query = query.Where(i => i.DetectedAt >= from);
        if (search.To is { } to) query = query.Where(i => i.DetectedAt <= to);
        if (search.ResolvedSince is { } resolvedSince) query = query.Where(i => i.ResolvedAt >= resolvedSince);
        if (!string.IsNullOrWhiteSpace(search.Text))
        {
            var pattern = $"%{search.Text.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.IncidentType, pattern) || EF.Functions.ILike(i.EvidenceSummary, pattern));
        }

        var total = await query.CountAsync(ct);
        var ordered = search.ResolvedSince is not null
            ? query.OrderByDescending(i => i.ResolvedAt).ThenBy(i => i.Id)
            : search.NewestFirst
                ? query.OrderByDescending(i => i.DetectedAt).ThenBy(i => i.Id)
                : query.OrderByDescending(i => i.Severity).ThenByDescending(i => i.DetectedAt).ThenBy(i => i.Id);
        var items = await ordered
            .Skip(search.Paging.Skip)
            .Take(search.Paging.PageSize)
            .ToListAsync(ct);
        return new PagedList<Incident>(items, total);
    }

    public async Task<IReadOnlyList<Incident>> GetBySourceObservationIdsAsync(IEnumerable<Guid> observationIds, CancellationToken ct = default)
    {
        var ids = observationIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await DbSet.Where(i => i.SourceObservationId != null && ids.Contains(i.SourceObservationId.Value)).ToListAsync(ct);
    }
}
