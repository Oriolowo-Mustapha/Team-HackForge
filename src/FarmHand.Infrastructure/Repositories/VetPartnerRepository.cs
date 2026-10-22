using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Repositories;

public class VetPartnerRepository : EfRepository<VetPartner>, IVetPartnerRepository
{
    public VetPartnerRepository(FarmHandDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<VetPartner>> GetBySpecializationAsync(string specialization, CancellationToken ct = default)
    {
        return await DbSet.Where(v => v.Specialization == specialization && v.IsActive).ToListAsync(ct);
    }

    public async Task<IEnumerable<VetPartner>> GetActiveAsync(CancellationToken ct = default)
    {
        return await DbSet.Where(v => v.IsActive && !v.IsDeleted).ToListAsync(ct);
    }

    public async Task<VetPartner?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(v => v.UserId == userId, ct);
    }
}
