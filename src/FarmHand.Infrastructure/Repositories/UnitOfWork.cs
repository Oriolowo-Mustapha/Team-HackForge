using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;

namespace FarmHand.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly FarmHandDbContext _dbContext;

    public IFarmRepository Farms { get; }
    public IFlockRepository Flocks { get; }
    public IPoultryObservationRepository PoultryObservations { get; }
    public IIncidentRepository Incidents { get; }
    public IVetRequestRepository VetRequests { get; }
    public ICostShareRepository CostShares { get; }
    public IPaymentRepository Payments { get; }
    public ITreatmentPlanRepository TreatmentPlans { get; }
    public IFollowUpRepository FollowUps { get; }
    public IPreventionPlanRepository PreventionPlans { get; }

    public UnitOfWork(FarmHandDbContext dbContext)
    {
        _dbContext = dbContext;
        Farms = new FarmRepository(dbContext);
        Flocks = new FlockRepository(dbContext);
        PoultryObservations = new PoultryObservationRepository(dbContext);
        Incidents = new IncidentRepository(dbContext);
        VetRequests = new VetRequestRepository(dbContext);
        CostShares = new CostShareRepository(dbContext);
        Payments = new PaymentRepository(dbContext);
        TreatmentPlans = new TreatmentPlanRepository(dbContext);
        FollowUps = new FollowUpRepository(dbContext);
        PreventionPlans = new PreventionPlanRepository(dbContext);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _dbContext.SaveChangesAsync(ct);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
