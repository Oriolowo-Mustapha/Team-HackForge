namespace FarmHand.Domain.Interfaces;

public interface ITreatmentPlanRepository : IRepository<TreatmentPlan>
{
    Task<TreatmentPlan?> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentPlan>> GetByVetRequestIdsAsync(IEnumerable<Guid> vetRequestIds, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentScheduleItem>> GetScheduleItemsAsync(IEnumerable<Guid> planIds, CancellationToken ct = default);
    Task<TreatmentScheduleItem?> GetScheduleItemAsync(Guid itemId, CancellationToken ct = default);
    Task AddScheduleItemAsync(TreatmentScheduleItem item, CancellationToken ct = default);
}
