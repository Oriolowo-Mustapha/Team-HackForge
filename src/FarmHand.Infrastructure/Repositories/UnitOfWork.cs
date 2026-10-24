using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmHand.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly FarmHandDbContext _dbContext;

    public IFarmRepository Farms { get; }
    public ILivestockCategoryRepository LivestockCategories { get; }
    public ILivestockAnimalRepository LivestockAnimals { get; }
    public IPoultryObservationRepository PoultryObservations { get; }
    public IIncidentRepository Incidents { get; }
    public IAIDiagnosisRepository AIDiagnoses { get; }
    public IVetRequestRepository VetRequests { get; }
    public ICostShareRepository CostShares { get; }
    public IPaymentRepository Payments { get; }
    public ITreatmentPlanRepository TreatmentPlans { get; }
    public IFollowUpRepository FollowUps { get; }
    public IPreventionPlanRepository PreventionPlans { get; }
    public IUserRepository Users { get; }
    public IVetPartnerRepository VetPartners { get; }
    public IIncidentActionRepository IncidentActions { get; }
    public IAuditEventRepository AuditEvents { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IPasswordResetTokenRepository PasswordResetTokens { get; }

    public UnitOfWork(FarmHandDbContext dbContext)
    {
        _dbContext = dbContext;
        Farms = new FarmRepository(dbContext);
        LivestockCategories = new LivestockCategoryRepository(dbContext);
        LivestockAnimals = new LivestockAnimalRepository(dbContext);
        PoultryObservations = new PoultryObservationRepository(dbContext);
        Incidents = new IncidentRepository(dbContext);
        AIDiagnoses = new AIDiagnosisRepository(dbContext);
        VetRequests = new VetRequestRepository(dbContext);
        CostShares = new CostShareRepository(dbContext);
        Payments = new PaymentRepository(dbContext);
        TreatmentPlans = new TreatmentPlanRepository(dbContext);
        FollowUps = new FollowUpRepository(dbContext);
        PreventionPlans = new PreventionPlanRepository(dbContext);
        Users = new UserRepository(dbContext);
        VetPartners = new VetPartnerRepository(dbContext);
        IncidentActions = new IncidentActionRepository(dbContext);
        AuditEvents = new AuditEventRepository(dbContext);
        RefreshTokens = new RefreshTokenRepository(dbContext);
        PasswordResetTokens = new PasswordResetTokenRepository(dbContext);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_VetRequests_OneActivePerIncident" })
        {
            throw PublicException.Conflict("This case already has an active vet request. Refresh to view it.");
        }
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
