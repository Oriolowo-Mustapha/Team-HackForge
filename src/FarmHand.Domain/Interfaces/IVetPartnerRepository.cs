namespace FarmHand.Domain.Interfaces;

public interface IVetPartnerRepository : IRepository<VetPartner>
{
    Task<IEnumerable<VetPartner>> GetBySpecializationAsync(string specialization, CancellationToken ct = default);
    Task<IEnumerable<VetPartner>> GetActiveAsync(CancellationToken ct = default);
    Task<VetPartner?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
