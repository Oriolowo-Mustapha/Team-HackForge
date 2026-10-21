namespace FarmHand.Domain.Interfaces;

public interface IPreventionPlanRepository : IRepository<PreventionPlan>
{
    Task<PreventionPlan?> GetByIncidentIdAsync(Guid incidentId, CancellationToken ct = default);
}
