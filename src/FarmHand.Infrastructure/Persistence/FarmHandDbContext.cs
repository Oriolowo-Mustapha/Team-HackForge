using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.Infrastructure.Persistence;

public class FarmHandDbContext : DbContext
{
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Flock> Flocks => Set<Flock>();
    public DbSet<PoultryObservation> PoultryObservations => Set<PoultryObservation>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAction> IncidentActions => Set<IncidentAction>();
    public DbSet<VetPartner> VetPartners => Set<VetPartner>();
    public DbSet<VetRequest> VetRequests => Set<VetRequest>();
    public DbSet<CostShare> CostShares => Set<CostShare>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<TreatmentScheduleItem> TreatmentScheduleItems => Set<TreatmentScheduleItem>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<PreventionPlan> PreventionPlans => Set<PreventionPlan>();
    public DbSet<LivestockRecord> LivestockRecords => Set<LivestockRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public FarmHandDbContext(DbContextOptions<FarmHandDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new FarmConfiguration());
        modelBuilder.ApplyConfiguration(new FlockConfiguration());
        modelBuilder.ApplyConfiguration(new PoultryObservationConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentActionConfiguration());
        modelBuilder.ApplyConfiguration(new VetPartnerConfiguration());
        modelBuilder.ApplyConfiguration(new VetRequestConfiguration());
        modelBuilder.ApplyConfiguration(new CostShareConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new TreatmentPlanConfiguration());
        modelBuilder.ApplyConfiguration(new TreatmentScheduleItemConfiguration());
        modelBuilder.ApplyConfiguration(new FollowUpConfiguration());
        modelBuilder.ApplyConfiguration(new PreventionPlanConfiguration());
        modelBuilder.ApplyConfiguration(new LivestockRecordConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await base.SaveChangesAsync(ct);
    }
}
