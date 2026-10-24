using FarmHand.API.Controllers;
using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Application.Common.Services;
using FarmHand.Application.Features.Incidents;
using FarmHand.Application.Features.Poultry;
using FarmHand.Application.Features.Payments;
using FarmHand.Application.Features.VetCoordination;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using FarmHand.Domain.ValueObjects;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Infrastructure.Repositories;
using FarmHand.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace FarmHand.IntegrationTests;

public class BackendBoundaryTests
{
    [DatabaseFact]
    public async Task IncidentSource_MustBeConfirmedAndMatchFarmAndSubject()
    {
        await using var scope = await Scope.Create();
        var foreignFarm = new Farm(Guid.NewGuid().ToString(), "Foreign synthetic farm", "Test");
        var foreign = new PoultryObservation(foreignFarm.Id, null, null, DateTime.UtcNow, 0, null, null, null, "PRIVATE FOREIGN REPORT");
        foreign.Confirm();
        scope.Db.Farms.Add(foreignFarm);
        scope.Db.PoultryObservations.Add(foreign);
        await scope.Db.SaveChangesAsync();
        var handler = new CreateIncidentCommandHandler(scope.Unit.Incidents, scope.Unit, scope.Authorization);
        var command = new CreateIncidentCommand(scope.Farm.Id, null, scope.Group.Id, "Synthetic case", Severity.High, "test", "test", SourceObservationId: foreign.Id);
        var denied = await Assert.ThrowsAsync<PublicException>(() => handler.Handle(command, default));
        Assert.Equal(400, denied.StatusCode);
        var pending = scope.Observation();
        scope.Db.PoultryObservations.Add(pending);
        await scope.Db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<PublicException>(() => handler.Handle(command with { SourceObservationId = pending.Id }, default))).StatusCode);
        pending.Confirm();
        await scope.Db.SaveChangesAsync();
        var id = await handler.Handle(command with { SourceObservationId = pending.Id }, default);
        Assert.Equal(pending.Id, (await scope.Db.Incidents.SingleAsync(i => i.Id == id)).SourceObservationId);
        Assert.False(await scope.Db.Incidents.AnyAsync(i => i.SourceObservationId == foreign.Id));
    }

    [DatabaseFact]
    public async Task Confirmation_DownwardOverrideCannotSuppressCase_AndRetryDoesNotDuplicate()
    {
        await using var scope = await Scope.Create();
        var observation = scope.Observation("Gasping with breathing difficulty", mortality: 0);
        scope.Db.PoultryObservations.Add(observation);
        await scope.Db.SaveChangesAsync();
        var handler = scope.ConfirmHandler();
        var result = await handler.Handle(new ConfirmObservationCommand(observation.Id, null, Severity.Low), default);
        Assert.Equal("High", result.FinalSeverity);
        Assert.True(result.AiRequiresVetConsultation);
        Assert.True(result.IncidentAutoCreated);
        scope.Db.ChangeTracker.Clear();
        var persisted = await scope.Db.AIDiagnoses.SingleAsync(d => d.ObservationId == observation.Id);
        Assert.Equal(result.AutoCreatedIncidentId, persisted.IncidentId);
        var repeated = await handler.Handle(new ConfirmObservationCommand(observation.Id, null), default);
        Assert.Equal(result.AutoCreatedIncidentId, repeated.AutoCreatedIncidentId);
        Assert.Equal(1, await scope.Db.Incidents.CountAsync(i => i.SourceObservationId == observation.Id));
    }

    [DatabaseFact]
    public async Task HistoricalReportProjection_ReplacesStoredUnsafeAssessmentAdviceOnRead()
    {
        await using var scope = await Scope.Create();
        var observation = scope.Observation("Gasping");
        observation.Confirm();
        var diagnosis = new AIDiagnosis(observation.Id, Severity.Low, "Confirmed Newcastle", "Inject 5 ml", false,
            "raw", "gemini:old", "[\"Give 50 mg\"]");
        scope.Db.PoultryObservations.Add(observation);
        scope.Db.AIDiagnoses.Add(diagnosis);
        await scope.Db.SaveChangesAsync();
        var displayed = await new ObservationProjector(scope.Unit).DetailAsync(observation, false, default);
        Assert.True(displayed.Assessment!.RequiresVetConsultation);
        Assert.DoesNotContain("Newcastle", displayed.Assessment.Summary);
        Assert.DoesNotContain("5 ml", displayed.Assessment.RecommendedAction);
        scope.Db.ChangeTracker.Clear();
        Assert.Equal("Inject 5 ml", (await scope.Db.AIDiagnoses.SingleAsync(d => d.Id == diagnosis.Id)).RecommendedAction);
    }

    [DatabaseFact]
    public async Task FarmerRaisedUrgency_PreservesCriticalEscalationOnReportReadAndConfirmRetry()
    {
        await using var scope = await Scope.Create();
        var observation = scope.Observation("Routine observation without known red flags");
        scope.Db.PoultryObservations.Add(observation);
        await scope.Db.SaveChangesAsync();
        var handler = scope.ConfirmHandler();
        var result = await handler.Handle(new ConfirmObservationCommand(observation.Id, null, Severity.Critical), default);
        Assert.Equal("Low", result.AiSeverity);
        Assert.Equal("Critical", result.FinalSeverity);
        scope.Db.ChangeTracker.Clear();
        var saved = await scope.Db.PoultryObservations.SingleAsync(o => o.Id == observation.Id);
        var detail = await new ObservationProjector(scope.Unit).DetailAsync(saved, false, default);
        Assert.Equal(Severity.Critical, detail.Assessment!.Severity);
        Assert.True(detail.Assessment.RequiresVetConsultation);
        Assert.Contains("contact a veterinarian today", detail.Assessment.RecommendedAction);
        var repeated = await handler.Handle(new ConfirmObservationCommand(observation.Id, null), default);
        Assert.Equal("Critical", repeated.FinalSeverity);
        Assert.True(repeated.AiRequiresVetConsultation);
        Assert.Equal(result.AutoCreatedIncidentId, repeated.AutoCreatedIncidentId);
    }

    [DatabaseFact]
    public async Task Confirmation_FailedFinalWriteRollsBackReportAssessmentAndCaseTogether()
    {
        await using var scope = await Scope.Create();
        var observation = scope.Observation("Gasping");
        scope.Db.PoultryObservations.Add(observation);
        await scope.Db.SaveChangesAsync();
        // An actual database constraint failure at final save distinguishes one transaction from
        // the previous implementation, which committed the report and assessment before the case.
        var handler = scope.ConfirmHandler(new InvalidAudit(scope.Db));
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(new ConfirmObservationCommand(observation.Id, null), default));
        scope.Db.ChangeTracker.Clear();
        Assert.True((await scope.Db.PoultryObservations.SingleAsync(o => o.Id == observation.Id)).IsPending);
        Assert.False(await scope.Db.AIDiagnoses.AnyAsync(d => d.ObservationId == observation.Id));
        Assert.False(await scope.Db.Incidents.AnyAsync(i => i.SourceObservationId == observation.Id));
    }

    [DatabaseFact]
    public async Task RequestCancellation_PreservesPendingLiveCheckoutForProviderReconciliation()
    {
        await using var scope = await Scope.Create();
        var incident = scope.Incident();
        var request = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000));
        request.Respond(Guid.NewGuid());
        var payment = new Payment(request.Id, new Money(20000), 5, PaymentMode.Live);
        payment.MarkPending("synthetic-live-not-a-provider-reference");
        scope.Db.Incidents.Add(incident);
        scope.Db.VetRequests.Add(request);
        scope.Db.Payments.Add(payment);
        await scope.Db.SaveChangesAsync();
        var handler = new CancelVetRequestCommandHandler(scope.Unit, scope.Authorization, scope.Audit);
        var denied = await Assert.ThrowsAsync<PublicException>(() => handler.Handle(new CancelVetRequestCommand(request.Id), default));
        Assert.Equal(409, denied.StatusCode);
        scope.Db.ChangeTracker.Clear();
        Assert.Equal(PaymentStatus.Pending, (await scope.Db.Payments.SingleAsync(p => p.Id == payment.Id)).Status);
        Assert.Equal(RequestStatus.Responded, (await scope.Db.VetRequests.SingleAsync(r => r.Id == request.Id)).Status);
    }

    [DatabaseFact]
    public async Task LiveCheckoutCancellation_CannotCloseUnresolvedHostedCheckout()
    {
        foreach (var outcome in new[] { PaymentVerificationOutcome.Pending, PaymentVerificationOutcome.NotFound })
        {
            await using var scope = await Scope.Create();
            var incident = scope.Incident();
            var request = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000));
            request.Respond(Guid.NewGuid());
            var payment = new Payment(request.Id, new Money(20000), 5, PaymentMode.Live);
            payment.MarkPending($"synthetic-{Guid.NewGuid():N}");
            scope.Db.Incidents.Add(incident);
            scope.Db.VetRequests.Add(request);
            scope.Db.Payments.Add(payment);
            await scope.Db.SaveChangesAsync();
            var handler = new CancelPaymentCommandHandler(scope.Unit, new PendingProvider(outcome), scope.Authorization,
                new PaymentSettlement(scope.Audit), scope.Audit);
            var error = await Assert.ThrowsAsync<PublicException>(() => handler.Handle(new CancelPaymentCommand(payment.TransactionReference), default));
            Assert.Equal(409, error.StatusCode);
            scope.Db.ChangeTracker.Clear();
            Assert.Equal(PaymentStatus.Pending, (await scope.Db.Payments.SingleAsync(p => p.Id == payment.Id)).Status);
        }
    }

    [DatabaseFact]
    public async Task VetCaseSearch_IncludesAssignmentsBeyondFirstHundred_AndPagesStably()
    {
        await using var scope = await Scope.Create();
        var vet = Guid.NewGuid();
        for (var index = 0; index < 105; index++)
        {
            var incident = scope.Incident();
            var request = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000));
            request.Respond(vet);
            scope.Db.Incidents.Add(incident);
            scope.Db.VetRequests.Add(request);
        }
        scope.Db.Incidents.Add(scope.Incident()); // Same farm, no assignment: must not leak into scope.
        await scope.Db.SaveChangesAsync();
        var repository = new IncidentRepository(scope.Db);
        var search = new IncidentSearch(null, null, null, null, false, null, null, null, null, null, new Paging(1, 100), vet);
        var first = await repository.SearchAsync(search);
        var second = await repository.SearchAsync(search with { Paging = new Paging(2, 100) });
        Assert.Equal(105, first.TotalCount);
        Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        Assert.Empty((await repository.SearchAsync(search with { AssignedVetPartnerId = Guid.NewGuid() })).Items);
        Assert.Equal(int.MaxValue, new Paging(int.MaxValue, 100).Skip);
    }

    [DatabaseFact]
    public async Task LocalMedia_RejectsOtherFarmerAndAllowsOwnerAndAssignedVetOnly()
    {
        await using var scope = await Scope.Create();
        var root = Path.Combine(Path.GetTempPath(), $"farmhand-media-test-{Guid.NewGuid():N}");
        var storage = new LocalImageStorage(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:LocalPath"] = root }).Build());
        try
        {
            var image = await storage.StoreAsync([0xFF, 0xD8, 0xFF], "image/jpeg", "test");
            var observation = new PoultryObservation(scope.Farm.Id, null, scope.Group.Id, DateTime.UtcNow, 0, null, null, null, "Synthetic", image.Url);
            observation.Confirm();
            var incident = scope.Incident(observation.Id);
            var user = new User($"media-vet-{Guid.NewGuid():N}@example.invalid", "synthetic-hash", UserRoles.Vet);
            var vet = new VetPartner(user.Id, "Synthetic vet", user.Email, "Poultry", "Test");
            var request = new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000), vet.Id);
            scope.Db.Users.Add(user);
            scope.Db.VetPartners.Add(vet);
            scope.Db.PoultryObservations.Add(observation);
            scope.Db.Incidents.Add(incident);
            scope.Db.VetRequests.Add(request);
            await scope.Db.SaveChangesAsync();
            var key = image.Url.Split('/').Last();
            MediaController Media(IResourceAuthorizationService auth) => new(storage, scope.Db, auth)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            Assert.IsType<PhysicalFileResult>(await Media(scope.Authorization).Get(key, default));
            var other = new ResourceAuthorizationService(new Current(Guid.NewGuid()), scope.Unit);
            Assert.IsType<NotFoundResult>(await Media(other).Get(key, default));
            var vetAuth = new ResourceAuthorizationService(new Current(user.Id, UserRoles.Vet), scope.Unit);
            Assert.IsType<NotFoundResult>(await Media(vetAuth).Get(key, default));
            request.Respond(vet.Id);
            await scope.Db.SaveChangesAsync();
            Assert.IsType<PhysicalFileResult>(await Media(vetAuth).Get(key, default));
        }
        finally
        {
            // Delete only the newly-created test directory under the OS temp directory.
            var safeRoot = Path.GetFullPath(root);
            if (!safeRoot.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test cleanup must remain inside the OS temp directory.");
            if (Directory.Exists(safeRoot)) Directory.Delete(safeRoot, recursive: true);
        }
    }

    [DatabaseFact]
    public async Task ConcurrentActiveRequests_ExactlyOneIsStored_AndLoserReceivesConflict()
    {
        var options = Options();
        await using var setup = new FarmHandDbContext(options);
        await setup.Database.MigrateAsync();
        var farm = new Farm(Guid.NewGuid().ToString(), "Synthetic concurrency farm", "Test");
        var incident = new Incident(farm.Id, null, null, DateTime.UtcNow, "Test", Severity.High, "test", "test");
        setup.Farms.Add(farm);
        setup.Incidents.Add(incident);
        await setup.SaveChangesAsync();
        try
        {
            async Task<int> Insert()
            {
                await using var db = new FarmHandDbContext(options);
                var unit = new UnitOfWork(db);
                await unit.VetRequests.AddAsync(new VetRequest(incident.Id, RequestType.RemoteReview, new Money(20000)));
                try { await unit.SaveChangesAsync(); return 201; }
                catch (PublicException ex) { return ex.StatusCode; }
            }
            var results = await Task.WhenAll(Insert(), Insert());
            Assert.Equal(new[] { 201, 409 }, results.OrderBy(r => r));
            Assert.Equal(1, await setup.VetRequests.CountAsync(r => r.IncidentId == incident.Id && r.Status == RequestStatus.Pending));
        }
        finally { await setup.Farms.Where(f => f.Id == farm.Id).ExecuteDeleteAsync(); }
    }

    private static DbContextOptions<FarmHandDbContext> Options() => new DbContextOptionsBuilder<FarmHandDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options;

    private sealed class Current(Guid id, string role = UserRoles.Farmer) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? Email => "synthetic@example.invalid";
        public string? Role => role;
        public bool IsAdmin => role == UserRoles.Admin;
        public bool IsVet => role == UserRoles.Vet;
    }

    private sealed class Scope : IAsyncDisposable
    {
        public required FarmHandDbContext Db { get; init; }
        public required IDbContextTransaction Transaction { get; init; }
        public required Farm Farm { get; init; }
        public required LivestockCategory Group { get; init; }
        public required UnitOfWork Unit { get; init; }
        public required IResourceAuthorizationService Authorization { get; init; }
        public required IAuditLogger Audit { get; init; }
        public static async Task<Scope> Create()
        {
            var db = new FarmHandDbContext(Options());
            await db.Database.MigrateAsync();
            var transaction = await db.Database.BeginTransactionAsync();
            var current = new Current(Guid.NewGuid());
            var farm = new Farm(current.UserId!.Value.ToString(), "Synthetic boundary farm", "Test");
            var group = new LivestockCategory(farm.Id, "Synthetic group", species: "Chicken");
            db.Farms.Add(farm);
            db.LivestockCategories.Add(group);
            await db.SaveChangesAsync();
            var unit = new UnitOfWork(db);
            return new Scope { Db = db, Transaction = transaction, Farm = farm, Group = group, Unit = unit,
                Authorization = new ResourceAuthorizationService(current, unit), Audit = new AuditLogger(unit.AuditEvents, current) };
        }
        public PoultryObservation Observation(string notes = "Synthetic report", int mortality = 0) =>
            new(Farm.Id, null, Group.Id, DateTime.UtcNow, mortality, null, null, null, notes);
        public Incident Incident(Guid? source = null) =>
            new(Farm.Id, null, Group.Id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), "Synthetic", Severity.High, "test", "test", sourceObservationId: source);
        public ConfirmObservationCommandHandler ConfirmHandler(IAuditLogger? audit = null) => new(Unit.PoultryObservations,
            Unit.Incidents, Unit.AIDiagnoses, new LowAi(), new NoHistory(), new NoVoice(), new NoVoice(), Unit, Authorization, audit ?? Audit);
        public async ValueTask DisposeAsync() { await Transaction.RollbackAsync(); await Transaction.DisposeAsync(); await Db.DisposeAsync(); }
    }

    private sealed class LowAi : IFarmAiService
    {
        public bool IsAiConfigured => true;
        public Task<ObservationExtraction> ExtractObservationAsync(string rawText, string? subject, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AIAssessment> AssessSeverityAsync(AssessmentInput input, CancellationToken ct = default) =>
            Task.FromResult(new AIAssessment(Severity.Low, "Unsafe provider text", "Give drugs", false, "unsafe", "gemini:test"));
    }
    private sealed class NoHistory : IFarmHistoryService
    {
        public Task<FarmHistoryContext> GetFarmHistoryAsync(Guid farmId, CancellationToken ct = default) => Task.FromResult(new FarmHistoryContext([], [], []));
    }
    private sealed class NoVoice : ITtsService, IVoiceReplySettings
    {
        public bool IsConfigured => false;
        public bool RespondWithVoice => false;
        public Task<TtsResult?> SynthesizeAsync(string text, string? language, CancellationToken ct = default) => Task.FromResult<TtsResult?>(null);
    }
    private sealed class InvalidAudit(FarmHandDbContext db) : IAuditLogger
    {
        public Task RecordAsync(string eventType, Guid? relatedRecordId = null, string? details = null, string? actorIdOverride = null, CancellationToken ct = default)
        {
            db.AuditEvents.Add(new AuditEvent("synthetic", eventType, relatedRecordId, new string('x', 1100)));
            return Task.CompletedTask;
        }
    }

    private sealed class PendingProvider(PaymentVerificationOutcome outcome) : IPaymentService
    {
        public string Mode => "Live";
        public Task<PaymentVerification> VerifyByReferenceAsync(string reference, CancellationToken ct = default) =>
            Task.FromResult(new PaymentVerification(outcome, null, null, null, reference, "Synthetic pending provider"));
        public Task<PaymentResult> InitiatePaymentAsync(Guid id, decimal amount, string currency, string email, string? name = null, PaymentSplit? split = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentVerification> VerifyByTransactionIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SubaccountResult> CreateOrUpdateSubaccountAsync(VetSubaccountRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public bool VerifyWebhookSignature(string body, string? signature, string? hash, string secret) => false;
    }
}
