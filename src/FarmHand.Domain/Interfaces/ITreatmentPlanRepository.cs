namespace FarmHand.Domain.Interfaces;

public interface ITreatmentPlanRepository : IRepository<TreatmentPlan>
{
    Task<TreatmentPlan?> GetByVetRequestIdAsync(Guid vetRequestId, CancellationToken ct = default);
}
