namespace FarmHand.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IFarmRepository Farms { get; }
    IFlockRepository Flocks { get; }
    IPoultryObservationRepository PoultryObservations { get; }
    IIncidentRepository Incidents { get; }
    IVetRequestRepository VetRequests { get; }
    ICostShareRepository CostShares { get; }
    IPaymentRepository Payments { get; }
    ITreatmentPlanRepository TreatmentPlans { get; }
    IFollowUpRepository FollowUps { get; }
    IPreventionPlanRepository PreventionPlans { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
