using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class LivestockCategoryRepository : EfRepository<LivestockCategory>, ILivestockCategoryRepository
{
    public LivestockCategoryRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<LivestockCategory>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(c => c.FarmId == farmId && !c.IsDeleted).ToListAsync(ct);
    }
}

public class LivestockAnimalRepository : EfRepository<LivestockAnimal>, ILivestockAnimalRepository
{
    public LivestockAnimalRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<LivestockAnimal>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.CategoryId == categoryId && a.IsActive && !a.IsDeleted).ToListAsync(ct);
    }

    public async Task<IEnumerable<LivestockAnimal>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.FarmId == farmId && a.IsActive && !a.IsDeleted).ToListAsync(ct);
    }

    public async Task<IEnumerable<LivestockAnimal>> GetBySpeciesAsync(Guid farmId, string species, CancellationToken ct = default)
    {
        return await DbSet.Where(a => a.FarmId == farmId && a.Species == species && a.IsActive && !a.IsDeleted).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LivestockAnimal>> SearchAsync(Guid farmId, Guid? categoryId, string? text, string? species, bool includeInactive, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().Where(a => a.FarmId == farmId);
        if (!includeInactive) query = query.Where(a => a.IsActive);
        if (categoryId is { } category) query = query.Where(a => a.CategoryId == category);
        if (!string.IsNullOrWhiteSpace(species)) query = query.Where(a => a.Species == species);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = $"%{text.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Name, pattern) || (a.Breed != null && EF.Functions.ILike(a.Breed, pattern)));
        }

        return await query.OrderBy(a => a.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountActiveByCategoryAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(a => a.FarmId == farmId && a.IsActive)
            .GroupBy(a => a.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}

public class PoultryObservationRepository : EfRepository<PoultryObservation>, IPoultryObservationRepository
{
    public PoultryObservationRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IReadOnlyList<PoultryObservation>> GetRecentForAnimalAsync(Guid animalId, int take, CancellationToken ct = default)
        => await DbSet.AsNoTracking().Where(o => o.LivestockAnimalId == animalId)
            .OrderByDescending(o => o.RecordedAt).ThenBy(o => o.Id).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);

    public async Task<IEnumerable<PoultryObservation>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default)
    {
        return await DbSet.Where(o => o.LivestockAnimalId == animalId).OrderByDescending(o => o.RecordedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<PoultryObservation>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
    {
        return await DbSet.Where(o => o.LivestockCategoryId == categoryId).OrderByDescending(o => o.RecordedAt).ToListAsync(ct);
    }

    public async Task<IEnumerable<PoultryObservation>> GetRecentByAnimalIdAsync(Guid animalId, int days, CancellationToken ct = default)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);
        return await DbSet.Where(o => o.LivestockAnimalId == animalId && o.RecordedAt >= fromDate).ToListAsync(ct);
    }

    public async Task<bool> ExistsUnconfirmedAsync(Guid animalId, DateTime recordedAt, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(o => o.LivestockAnimalId == animalId && o.RecordedAt.Date == recordedAt.Date && o.ConfirmationStatus != Domain.Enums.ObservationConfirmationStatus.Confirmed, ct);
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(o => o.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<PoultryObservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
    }

    public async Task<PagedList<PoultryObservation>> SearchAsync(ObservationSearch search, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().Where(o => search.FarmIds.Contains(o.FarmId));
        if (search.Status is { } status) query = query.Where(o => o.ConfirmationStatus == status);
        if (search.AnimalId is { } animalId) query = query.Where(o => o.LivestockAnimalId == animalId);
        if (search.CategoryId is { } categoryId) query = query.Where(o => o.LivestockCategoryId == categoryId);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.RecordedAt).ThenBy(o => o.Id).Skip(search.Paging.Skip).Take(search.Paging.PageSize).ToListAsync(ct);
        return new PagedList<PoultryObservation>(items, total);
    }

    public async Task<IReadOnlyList<PoultryObservation>> GetByFarmSinceAsync(Guid farmId, DateTime since, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().Where(o => o.FarmId == farmId && o.RecordedAt >= since).OrderByDescending(o => o.RecordedAt).ToListAsync(ct);
    }

    public async Task<int> CountPendingAsync(Guid farmId, CancellationToken ct = default)
    {
        return await DbSet.CountAsync(o => o.FarmId == farmId && o.ConfirmationStatus == Domain.Enums.ObservationConfirmationStatus.Pending, ct);
    }

    public async Task<IReadOnlyList<PoultryObservation>> GetRecentByFarmAsync(Guid farmId, int take, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking().Where(o => o.FarmId == farmId).OrderByDescending(o => o.RecordedAt).Take(take).ToListAsync(ct);
    }
}
