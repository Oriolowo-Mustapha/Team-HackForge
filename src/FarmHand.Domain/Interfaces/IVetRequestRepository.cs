namespace FarmHand.Domain.Interfaces;

public interface IVetRequestRepository : IRepository<VetRequest>
{
    Task<IEnumerable<VetRequest>> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
    Task<VetRequest?> GetByIdWithDetailsAsync(Guid vetRequestId, CancellationToken ct = default);
}
