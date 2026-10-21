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
}

public class PoultryObservationRepository : EfRepository<PoultryObservation>, IPoultryObservationRepository
{
    public PoultryObservationRepository(FarmHandDbContext dbContext) : base(dbContext) { }

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
}
