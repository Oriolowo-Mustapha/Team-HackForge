namespace FarmHand.Domain.Interfaces;

public interface ILivestockCategoryRepository : IRepository<LivestockCategory>
{
    Task<IEnumerable<LivestockCategory>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
}

public interface ILivestockAnimalRepository : IRepository<LivestockAnimal>
{
    Task<IEnumerable<LivestockAnimal>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
    Task<IEnumerable<LivestockAnimal>> GetByFarmIdAsync(Guid farmId, CancellationToken ct = default);
    Task<IEnumerable<LivestockAnimal>> GetBySpeciesAsync(Guid farmId, string species, CancellationToken ct = default);
}
