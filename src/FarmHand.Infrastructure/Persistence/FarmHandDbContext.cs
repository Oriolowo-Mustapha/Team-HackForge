using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Persistence;

public class FarmHandDbContext : DbContext
{
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<LivestockCategory> LivestockCategories => Set<LivestockCategory>();
    public DbSet<LivestockAnimal> LivestockAnimals => Set<LivestockAnimal>();
    public DbSet<PoultryObservation> PoultryObservations => Set<PoultryObservation>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAction> IncidentActions => Set<IncidentAction>();
    public DbSet<VetPartner> VetPartners => Set<VetPartner>();
    public DbSet<VetRequest> VetRequests => Set<VetRequest>();
    public DbSet<CostShare> CostShares => Set<CostShare>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AIDiagnosis> AIDiagnoses => Set<AIDiagnosis>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<TreatmentScheduleItem> TreatmentScheduleItems => Set<TreatmentScheduleItem>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<PreventionPlan> PreventionPlans => Set<PreventionPlan>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public FarmHandDbContext(DbContextOptions<FarmHandDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new LivestockCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new LivestockAnimalConfiguration());
        modelBuilder.ApplyConfiguration(new PoultryObservationConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentActionConfiguration());
        modelBuilder.ApplyConfiguration(new VetPartnerConfiguration());
        modelBuilder.ApplyConfiguration(new VetRequestConfiguration());
        modelBuilder.ApplyConfiguration(new CostShareConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new AIDiagnosisConfiguration());
        modelBuilder.ApplyConfiguration(new TreatmentPlanConfiguration());
        modelBuilder.ApplyConfiguration(new TreatmentScheduleItemConfiguration());
        modelBuilder.ApplyConfiguration(new FollowUpConfiguration());
        modelBuilder.ApplyConfiguration(new PreventionPlanConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());

        // Soft delete is applied consistently: rows flagged IsDeleted are hidden from every query.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || !typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var body = System.Linq.Expressions.Expression.Not(
                System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted)));
            entityType.SetQueryFilter(System.Linq.Expressions.Expression.Lambda(body, parameter));
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await base.SaveChangesAsync(ct);
    }

    /// <summary>Raw access to this context's database facade for health checks.</summary>
    public Task<bool> CanConnectAsync(CancellationToken ct = default) => Database.CanConnectAsync(ct);
}
