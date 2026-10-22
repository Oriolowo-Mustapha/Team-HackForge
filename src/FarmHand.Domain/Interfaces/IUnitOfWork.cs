namespace FarmHand.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IFarmRepository Farms { get; }
    ILivestockCategoryRepository LivestockCategories { get; }
    ILivestockAnimalRepository LivestockAnimals { get; }
    IPoultryObservationRepository PoultryObservations { get; }
    IIncidentRepository Incidents { get; }
    IAIDiagnosisRepository AIDiagnoses { get; }
    IVetRequestRepository VetRequests { get; }
    ICostShareRepository CostShares { get; }
    IPaymentRepository Payments { get; }
    ITreatmentPlanRepository TreatmentPlans { get; }
    IFollowUpRepository FollowUps { get; }
    IPreventionPlanRepository PreventionPlans { get; }
    IUserRepository Users { get; }
    IVetPartnerRepository VetPartners { get; }
    IIncidentActionRepository IncidentActions { get; }
    IAuditEventRepository AuditEvents { get; }
    IRefreshTokenRepository RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
