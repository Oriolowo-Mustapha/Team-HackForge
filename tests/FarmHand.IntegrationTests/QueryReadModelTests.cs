using FarmHand.Domain.Entities;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Application.Features.Farms;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.ValueObjects;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.Repositories;
using FarmHand.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FarmHand.IntegrationTests;

public class QueryReadModelTests
{
    [DatabaseFact]
    public async Task FarmQueries_KeepOlderActiveRequestsAndReminders_AndAggregateScopedCounts()
    {
        var options = new DbContextOptionsBuilder<FarmHandDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options;
        await using var db = new FarmHandDbContext(options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var current = new Current(Guid.NewGuid());
        var farm = new Farm(current.UserId!.Value.ToString(), "Synthetic query test", "Test only");
        var otherFarm = new Farm("other-query-owner", "Unrelated synthetic farm", "Test only");
        db.Farms.AddRange(farm, otherFarm);
        var group = new LivestockCategory(farm.Id, "Test group", species: "Chicken", headcount: 123);
        var animal = new LivestockAnimal(farm.Id, group.Id, "Test animal", "Chicken");
        db.LivestockCategories.Add(group);
        db.LivestockAnimals.Add(animal);
        var incident = new Incident(farm.Id, animal.Id, group.Id, DateTime.UtcNow, "Synthetic case", Severity.High, "test", "test");
        db.Incidents.Add(incident);
        var oldestActive = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000));
        oldestActive.Respond();
        db.VetRequests.Add(oldestActive);
        var baselineTotals = await new PlatformStatsRepository(db).GetOverviewAsync();
        var livePayment = new Payment(oldestActive.Id, new Money(20000), 5, PaymentMode.Live);
        livePayment.MarkPending($"query-live-{Guid.NewGuid():N}");
        livePayment.MarkCompleted("synthetic-provider-reference");
        var simulatedPayment = new Payment(oldestActive.Id, new Money(10000), 5, PaymentMode.Simulated);
        simulatedPayment.MarkPending($"query-simulated-{Guid.NewGuid():N}");
        simulatedPayment.MarkCompleted("synthetic-simulator-reference");
        db.Payments.AddRange(livePayment, simulatedPayment);
        // More than the former dashboard's history window; terminal rows cannot hide active work.
        for (var index = 0; index < 55; index++)
        {
            var terminal = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000));
            terminal.Cancel();
            db.VetRequests.Add(terminal);
        }
        var plan = new TreatmentPlan(oldestActive.Id, "synthetic-vet", "Synthetic follow-up only");
        var due = new TreatmentScheduleItem(plan.Id, "Record appetite", DateTime.UtcNow.AddDays(-1));
        db.TreatmentPlans.Add(plan);
        db.TreatmentScheduleItems.Add(due);
        for (var index = 0; index < 35; index++)
            db.PoultryObservations.Add(new PoultryObservation(farm.Id, animal.Id, group.Id,
                DateTime.UtcNow.AddMinutes(-index), 0, null, null, null, "Synthetic observation"));
        db.PoultryObservations.Add(new PoultryObservation(otherFarm.Id, null, null, DateTime.UtcNow, 0, null, null, null, "Foreign observation"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var counts = (await new FarmRepository(db).GetRecordCountsAsync([farm.Id], DateTime.UtcNow.AddDays(-30)))[farm.Id];
        Assert.Equal(1, counts.Groups);
        Assert.Equal(1, counts.Animals);
        Assert.Equal(123, counts.Headcount);
        Assert.Equal(1, counts.OpenIncidents);
        Assert.Equal(35, counts.PendingObservations);
        Assert.Equal(1, counts.Incidents);
        var active = await new VetRequestRepository(db).GetActiveByFarmAsync(farm.Id);
        Assert.Equal(oldestActive.Id, Assert.Single(active).Request.Id);
        var reminders = await new TreatmentPlanRepository(db).GetPendingByFarmAsync(farm.Id, 6);
        Assert.Equal("Record appetite", Assert.Single(reminders).Action);
        Assert.Empty(await new TreatmentPlanRepository(db).GetPendingByFarmAsync(otherFarm.Id, 6));
        var observations = new PoultryObservationRepository(db);
        Assert.Equal(30, (await observations.GetRecentForAnimalAsync(animal.Id, 30)).Count);
        var first = await observations.SearchAsync(new ObservationSearch([farm.Id], null, animal.Id, null, new Paging(1, 20)));
        var second = await observations.SearchAsync(new ObservationSearch([farm.Id], null, animal.Id, null, new Paging(2, 20)));
        Assert.Equal(35, first.TotalCount);
        Assert.Equal(15, second.Items.Count);
        Assert.Empty(first.Items.Select(o => o.Id).Intersect(second.Items.Select(o => o.Id)));
        var totals = await new PlatformStatsRepository(db).GetOverviewAsync();
        Assert.Equal(baselineTotals.CompletedLivePaymentsTotal + 21000, totals.CompletedLivePaymentsTotal);
        Assert.Equal(baselineTotals.CompletedSimulatedPaymentsTotal + 10500, totals.CompletedSimulatedPaymentsTotal);
        Assert.Equal(baselineTotals.PlatformCommissionTotal + 1000, totals.PlatformCommissionTotal);
        await db.PoultryObservations.Where(o => o.FarmId == farm.Id).ExecuteUpdateAsync(set =>
            set.SetProperty(o => o.ConfirmationStatus, ObservationConfirmationStatus.Confirmed));
        var unit = new UnitOfWork(db);
        var authorization = new ResourceAuthorizationService(current, unit);
        var overview = await new GetFarmOverviewQueryHandler(unit, authorization,
            new ObservationProjector(unit), new VetRequestProjector(unit, current, authorization))
            .Handle(new GetFarmOverviewQuery(farm.Id), CancellationToken.None);
        Assert.Equal("follow_up", overview.NextActionKind);
        Assert.Equal("Paid", Assert.Single(overview.ActiveVetRequests).PaymentStatus);
        await transaction.RollbackAsync();
    }

    private sealed class Current(Guid id) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? Email => "query-test@example.invalid";
        public string? Role => "Farmer";
        public bool IsAdmin => false;
        public bool IsVet => false;
    }
}
