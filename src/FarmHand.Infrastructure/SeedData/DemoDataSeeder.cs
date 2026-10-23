using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.ValueObjects;
using FarmHand.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FarmHand.Infrastructure.SeedData;

public interface IDemoDataSeeder
{
    Task SeedAsync();
}

/// <summary>
/// Seeds a deterministic demonstration scenario for development and explicitly enabled demo
/// environments (<c>Demo:Enabled=true</c>). Never enable it on a production database.
/// Seeded assessments are labelled with source "demo-seed" so the UI never presents them as live AI output.
/// Re-running is safe: the scenario is only created once per database.
/// </summary>
public class DemoDataSeeder : IDemoDataSeeder
{
    public const string DemoSource = "demo-seed";
    public const string DemoFarmName = "Adeyemi Family Farm";

    private readonly FarmHandDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IFieldEncryptor _encryptor;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(FarmHandDbContext db, IConfiguration configuration, IFieldEncryptor encryptor, ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _encryptor = encryptor;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var password = _configuration["Seed:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seed:Password is required for demo seeding. Set a strong, non-placeholder password in appsettings, user secrets or environment variables.");

        string Email(string key, string fallback) => (_configuration[key] ?? fallback).Trim().ToLowerInvariant();
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        var admin = await EnsureUserAsync(Email("Seed:AdminEmail", "admin@farmhand.ai"), password, hash, UserRoles.Admin, "Platform Admin", "+2348030000001");
        var farmer = await EnsureUserAsync(Email("Seed:FarmerEmail", "farmer@farmhand.ai"), password, hash, UserRoles.Farmer, "Funmilayo Adeyemi", "+2348030000002");
        var vetUser = await EnsureUserAsync(Email("Seed:VetEmail", "vet@farmhand.ai"), password, hash, UserRoles.Vet, "Dr. Chinedu Okafor", "+2348030000003");
        var vet2User = await EnsureUserAsync(Email("Seed:Vet2Email", "vet2@farmhand.ai"), password, hash, UserRoles.Vet, "Dr. Halima Bello", "+2348030000004");
        var farmer2 = await EnsureUserAsync(Email("Seed:Farmer2Email", "farmer2@farmhand.ai"), password, hash, UserRoles.Farmer, "Ibrahim Musa", "+2348030000005");
        await _db.SaveChangesAsync();

        var okafor = await EnsureVetAsync(vetUser, "Dr. Chinedu Okafor", "Poultry health & flock biosecurity", "+234 803 000 0003 · Abeokuta, Ogun", withPayout: true);
        var bello = await EnsureVetAsync(vet2User, "Dr. Halima Bello", "Small ruminants (goats & sheep)", "+234 803 000 0004 · Ibadan, Oyo", withPayout: false);
        await _db.SaveChangesAsync();

        if (await _db.Farms.AnyAsync(f => f.OwnerId == farmer.Id.ToString() && f.DisplayName == DemoFarmName))
        {
            _logger.LogInformation("Demo scenario already present; skipping");
            return;
        }

        _logger.LogInformation("Seeding the FarmHand demo scenario");
        var now = DateTime.UtcNow;
        var today = now.Date;

        // ---- Farm, groups and animals --------------------------------------------------------
        var farm = new Farm(farmer.Id.ToString(), DemoFarmName, "Odeda, Ogun State");
        _db.Farms.Add(farm);
        Backdate(farm, today.AddDays(-60));
        farmer.AssignFarm(farm.Id);

        var layers = Group(farm, "Layer House A", "Chicken", 480, "Isa Brown layers, 34 weeks old", today.AddDays(-60));
        var broilers = Group(farm, "Broiler Pen 2", "Chicken", 300, "Cobb 500 broilers, day 21", today.AddDays(-21));
        var goats = Group(farm, "Goats", "Goat", 0, "West African Dwarf goats kept for breeding and milk", today.AddDays(-60));
        var sheep = Group(farm, "Sheep", "Sheep", 0, "Yankasa sheep", today.AddDays(-45));

        var adunni = Animal(farm, goats, "Adunni", "Goat", "West African Dwarf", 26, "Female", "Calm doe, good milker",
            disease: "Mild worm load treated in January", gestation: today.AddDays(-40), lastVet: today.AddDays(-75),
            vaccinations: "PPR (March), CDT booster (May)");
        Animal(farm, goats, "Bisi", "Goat", "West African Dwarf", 18, "Female", null, vaccinations: "PPR (March)");
        Animal(farm, goats, "Kunle", "Goat", "West African Dwarf", 30, "Male", "Breeding buck", vaccinations: "PPR (March)", parentage: "Sire: Ade (Odeda co-op)");
        Animal(farm, goats, "Ireti", "Goat", "West African Dwarf", 9, "Female", "Weaned kid of Adunni", parentage: "Dam: Adunni");
        Animal(farm, sheep, "Yankasa 07", "Sheep", "Yankasa", 20, "Female", "Ear tag 07");

        // ---- Daily flock checks for the last 14 days (chart data) -----------------------------
        int[] deaths = [0, 1, 0, 0, 2, 1, 0, 0, 1, 0, 0, 1, 0, 0];
        string[] eggs = ["402 eggs", "398 eggs", "405 eggs", "401 eggs", "389 eggs", "392 eggs", "400 eggs", "404 eggs", "399 eggs", "403 eggs", "406 eggs", "397 eggs", "402 eggs", "405 eggs"];
        for (var day = 13; day >= 1; day--)
        {
            var check = Observation(farm, layers, null, today.AddDays(-day).AddHours(7), deaths[13 - day],
                "normal", "normal", eggs[13 - day], "Routine morning check. Birds active, droppings normal.", ObservationConfirmationStatus.Confirmed);
            Diagnosis(check, Severity.Low, "Routine check with no warning signs reported.", "Continue daily checks and keep records.", null);
        }

        // ---- Case A: resolved respiratory case with the full lifecycle ------------------------
        var aObservation = Observation(farm, layers, null, today.AddDays(-17).AddHours(8), 6,
            "slightly reduced", "reduced", "about 340 eggs (down from 400)",
            "Several hens sneezing and gasping, some with watery eyes. Six hens died this week.",
            ObservationConfirmationStatus.Confirmed,
            transcript: "Mama Funmi voice note: some of the layers dey sneeze and they open mouth to breathe, six don die this week, eggs don drop.");
        var aIncident = Incident(farm, layers, null, aObservation, today.AddDays(-17).AddHours(9), "Respiratory signs in layers", Severity.High,
            "Layer House A: sneezing, gasping and a drop in egg production with six deaths in a week. Respiratory disease is possible; a veterinary examination is advised.");
        var aDiagnosis = Diagnosis(aObservation, Severity.High,
            "Sneezing, open-mouth breathing and a drop in egg production with rising deaths could indicate a respiratory infection spreading in the house.",
            "Isolate birds showing signs, improve ventilation, restrict visitors and request a veterinary examination.", aIncident);
        var aRequest = new VetRequest(aIncident.Id, RequestType.SharedVisit, new Money(15000), okafor.Id,
            "Please examine Layer House A. Birds are sneezing and some are gasping.", today.AddDays(-16), farmer.Id);
        _db.VetRequests.Add(aRequest);
        Backdate(aRequest, today.AddDays(-17).AddHours(9).AddMinutes(10));
        aRequest.Respond(okafor.Id);
        Set(aRequest, nameof(VetRequest.RespondedAt), today.AddDays(-17).AddHours(10));
        var aPayment = new Payment(aRequest.Id, new Money(15000), 5m, PaymentMode.Simulated, farmer.Id);
        aPayment.MarkPending($"FH_DEMO_{aRequest.Id:N}", null, $"demo-{aRequest.Id:N}", true, okafor.PaymentSubaccountId);
        aPayment.MarkCompleted();
        _db.Payments.Add(aPayment);
        Backdate(aPayment, today.AddDays(-17).AddHours(10).AddMinutes(20));
        Set(aPayment, nameof(Payment.PaidAt), today.AddDays(-17).AddHours(10).AddMinutes(25));
        var aPlan = new TreatmentPlan(aRequest.Id, okafor.Id.ToString(),
            "Separate birds with signs into the end pen. Give the prescribed water-soluble antibiotic for 5 days as directed on the label I left with you. Add vitamins to drinking water for 7 days. Open the side curtains during the day for ventilation and clean drinkers daily. Call me if deaths go above 3 in a day.",
            "en");
        _db.TreatmentPlans.Add(aPlan);
        Backdate(aPlan, today.AddDays(-16).AddHours(11));
        Step(aPlan, "Start medicated water (day 1 of 5)", today.AddDays(-16), done: today.AddDays(-16).AddHours(12));
        Step(aPlan, "Finish medicated water course", today.AddDays(-12), done: today.AddDays(-12).AddHours(9));
        Step(aPlan, "Vitamins in drinking water for 7 days", today.AddDays(-10), done: today.AddDays(-10).AddHours(8));
        aRequest.Complete("Visited and examined the flock. Treatment started; ventilation improved. Re-check in two weeks.");
        Set(aRequest, nameof(VetRequest.CompletedAt), today.AddDays(-16).AddHours(12));
        FollowUp(aIncident, Outcome.Improving, "Fewer birds sneezing. No deaths for two days.", today.AddDays(-13));
        FollowUp(aIncident, Outcome.Resolved, "No signs for a week and egg count back near 400.", today.AddDays(-9));
        var prevention = new PreventionPlan(aIncident.Id,
            "Keep a footbath at the entrance, limit visitors, quarantine new birds for 2 weeks, keep side curtains open by day, and record deaths and egg counts every morning.",
            generatedFromHistory: false);
        _db.PreventionPlans.Add(prevention);
        Backdate(prevention, today.AddDays(-15));
        aIncident.Acknowledge();
        Audit(farmer.Id, "incident.status_changed", aIncident.Id, "Open -> Acknowledged", today.AddDays(-17).AddHours(9).AddMinutes(5));
        aIncident.Resolve();
        Set(aIncident, nameof(FarmHand.Domain.Entities.Incident.ResolvedAt), today.AddDays(-9).AddHours(10));
        Audit(farmer.Id, "incident.status_changed", aIncident.Id, "Acknowledged -> Resolved", today.AddDays(-9).AddHours(10));
        Audit(farmer.Id, "payment.completed", aPayment.Id, "mode=Simulated; via=demo-seed", today.AddDays(-17).AddHours(10).AddMinutes(25));

        // ---- Case B: broilers with a follow-up due tomorrow -----------------------------------
        var bObservation = Observation(farm, broilers, null, today.AddDays(-3).AddHours(17), 1,
            "normal", "reduced", null, "Broilers eating less since yesterday and litter is wet near the drinkers. One bird died.",
            ObservationConfirmationStatus.Confirmed);
        var bIncident = Incident(farm, broilers, null, bObservation, today.AddDays(-3).AddHours(18), "Reduced feed intake in broilers", Severity.Medium,
            "Broiler Pen 2: lower feed intake and wet litter near drinkers with one death. Could be linked to leaking drinkers or early gut upset.");
        Diagnosis(bObservation, Severity.Medium,
            "Lower feed intake with wet litter can follow leaking drinkers or early gut upset. One death so far.",
            "Fix leaking drinkers, replace wet litter, and watch feed intake and droppings for 48 hours.", bIncident);
        _db.IncidentActions.Add(new IncidentAction(bIncident.Id, "Fix the leaking drinker line in Pen 2", "High"));
        _db.IncidentActions.Add(new IncidentAction(bIncident.Id, "Replace wet litter around drinkers", "Medium"));
        bIncident.Acknowledge();
        bIncident.RequireFollowUp(today.AddDays(1).AddHours(9));
        Audit(farmer.Id, "incident.status_changed", bIncident.Id, "Open -> Acknowledged", today.AddDays(-3).AddHours(18).AddMinutes(30));
        Audit(farmer.Id, "incident.status_changed", bIncident.Id, "Acknowledged -> FollowUpRequired", today.AddDays(-2).AddHours(8));

        // ---- Case C: Adunni the goat, open, waiting for a vet to accept ------------------------
        var cObservation = Observation(farm, goats, adunni, today.AddDays(-1).AddHours(16), 0,
            "drinking less", "not eating since morning", null,
            "Adunni's left side is swollen and tight, she is not eating and keeps lying down and getting up.",
            ObservationConfirmationStatus.Confirmed);
        var cIncident = Incident(farm, goats, adunni, cObservation, today.AddDays(-1).AddHours(16).AddMinutes(10), "Bloated belly in pregnant doe", Severity.High,
            "Adunni (pregnant WAD doe): swollen left flank, not eating, restless. Bloat can become an emergency; a vet should examine her today.");
        Diagnosis(cObservation, Severity.High,
            "A tight, swollen left side with loss of appetite and restlessness can indicate bloat, which can worsen quickly, especially in a pregnant doe.",
            "Stop feeding fresh legumes, keep her walking gently, and get a veterinarian to examine her today.", cIncident);
        var cRequest = new VetRequest(cIncident.Id, RequestType.SharedVisit, new Money(8000), null,
            "Pregnant doe with a swollen left side, not eating since this morning. Please come today if possible.", today, farmer.Id);
        _db.VetRequests.Add(cRequest);
        Backdate(cRequest, today.AddDays(-1).AddHours(16).AddMinutes(20));

        // ---- Pending voice report from this morning, waiting for review -----------------------
        var pending = Observation(farm, layers, null, now.AddMinutes(-50), 3,
            "normal", "slightly reduced", "about 385 eggs",
            "Three hens found dead this morning in Layer House A. A few others look sleepy with ruffled feathers.",
            ObservationConfirmationStatus.Pending,
            transcript: "Good morning. I found three hens dead this morning in Layer House A. A few others are sleepy and their feathers are ruffled. Eggs are a bit fewer, around 385.");
        pending.SetExtractionDetails("{\"Symptoms\":[\"Sudden deaths\",\"Weakness or lethargy\",\"Ruffled feathers\",\"Drop in egg production\"],\"MissingInformation\":[\"Are the birds eating and drinking normally?\",\"When did you first notice the sleepy birds?\"]}");
        pending.SetInputChannels("voice");
        pending.MarkAiExtracted(DemoSource);

        // ---- A second farm for cross-farm isolation checks ------------------------------------
        var otherFarm = new Farm(farmer2.Id.ToString(), "Musa Poultry Farm", "Zaria, Kaduna State");
        _db.Farms.Add(otherFarm);
        farmer2.AssignFarm(otherFarm.Id);
        var otherFlock = Group(otherFarm, "Broiler House 1", "Chicken", 1200, "Arbor Acres broilers", today.AddDays(-30));
        var otherObservation = Observation(otherFarm, otherFlock, null, today.AddDays(-2).AddHours(7), 14,
            "reduced", "reduced", null, "Fourteen birds died in two days with greenish droppings.", ObservationConfirmationStatus.Confirmed);
        var otherIncident = Incident(otherFarm, otherFlock, null, otherObservation, today.AddDays(-2).AddHours(8), "High mortality in broilers", Severity.Critical,
            "Broiler House 1: 14 deaths in two days with greenish droppings.");
        Diagnosis(otherObservation, Severity.Critical, "Rapid deaths with greenish droppings need urgent veterinary attention.", "Isolate the house and call a vet now.", otherIncident);

        await _db.SaveChangesAsync();
        _logger.LogInformation("Demo scenario seeded: farm {FarmId}", farm.Id);
    }

    private async Task<User> EnsureUserAsync(string email, string password, string passwordHash, string role, string fullName, string phoneNumber)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new User(email, passwordHash, role, fullName, phoneNumber);
            _db.Users.Add(user);
            _logger.LogInformation("Seeding demo {Role} user {Email}", role, email);
            return user;
        }

        var hashIsValid = false;
        try { hashIsValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash); }
        catch (Exception) { hashIsValid = false; }

        if (!hashIsValid)
            user.ChangePassword(BCrypt.Net.BCrypt.HashPassword(password));
        if (!user.IsActive)
            user.Activate();

        return user;
    }

    private async Task<VetPartner> EnsureVetAsync(User user, string name, string specialization, string contact, bool withPayout)
    {
        var vet = await _db.VetPartners.FirstOrDefaultAsync(v => v.UserId == user.Id);
        if (vet is null)
        {
            vet = new VetPartner(user.Id, name, user.Email, specialization, contact);
            _db.VetPartners.Add(vet);
        }
        else
        {
            vet.UpdateProfile(name, specialization, contact);
            if (!vet.IsActive) vet.Activate();
        }

        if (withPayout && !vet.HasPayoutAccount)
            vet.UpdatePayoutAccount("044", _encryptor.Encrypt("0123456789"), "6789", name, "NG", $"RS_SIM_{vet.Id:N}");

        return vet;
    }

    private LivestockCategory Group(Farm farm, string name, string species, int headcount, string description, DateTime created)
    {
        var group = new LivestockCategory(farm.Id, name, description, species, headcount);
        _db.LivestockCategories.Add(group);
        Backdate(group, created);
        return group;
    }

    private LivestockAnimal Animal(Farm farm, LivestockCategory group, string name, string species, string breed, int ageMonths, string sex, string? notes,
        string? disease = null, DateTime? gestation = null, DateTime? lastVet = null, string? vaccinations = null, string? parentage = null)
    {
        var animal = new LivestockAnimal(farm.Id, group.Id, name, species, breed, ageMonths, sex, notes, disease, gestation, lastVet, vaccinations, null, parentage);
        _db.LivestockAnimals.Add(animal);
        Backdate(animal, group.CreatedAt.AddDays(1));
        return animal;
    }

    private PoultryObservation Observation(Farm farm, LivestockCategory group, LivestockAnimal? animal, DateTime recordedAt, int deaths,
        string? water, string? feed, string? eggs, string notes, ObservationConfirmationStatus status, string? transcript = null)
    {
        var observation = new PoultryObservation(farm.Id, animal?.Id, group.Id, recordedAt, deaths, water, feed, eggs, notes);
        observation.MarkAiExtracted(DemoSource);
        observation.SetRawTranscript(transcript ?? notes);
        observation.SetInputChannels(transcript is null ? "text" : "voice");
        if (status == ObservationConfirmationStatus.Confirmed) observation.Confirm();
        _db.PoultryObservations.Add(observation);
        Backdate(observation, recordedAt);
        return observation;
    }

    private AIDiagnosis Diagnosis(PoultryObservation observation, Severity severity, string summary, string action, Incident? incident)
    {
        var diagnosis = new AIDiagnosis(observation.Id, severity, summary, action, severity >= Severity.High, null, DemoSource);
        if (incident is not null) diagnosis.LinkIncident(incident.Id);
        _db.AIDiagnoses.Add(diagnosis);
        Backdate(diagnosis, observation.RecordedAt.AddMinutes(5));
        return diagnosis;
    }

    private Incident Incident(Farm farm, LivestockCategory group, LivestockAnimal? animal, PoultryObservation source, DateTime detectedAt, string type, Severity severity, string evidence)
    {
        var incident = new Incident(farm.Id, animal?.Id, group.Id, detectedAt, type, severity, $"Observation {source.Id}", evidence, DemoSource, source.Id);
        _db.Incidents.Add(incident);
        Backdate(incident, detectedAt);
        return incident;
    }

    private void Step(TreatmentPlan plan, string action, DateTime due, DateTime? done)
    {
        var step = new TreatmentScheduleItem(plan.Id, action, due);
        if (done is not null) step.MarkCompleted();
        _db.TreatmentScheduleItems.Add(step);
        if (done is not null) Set(step, nameof(TreatmentScheduleItem.CompletedAt), done.Value);
    }

    private void FollowUp(Incident incident, Outcome outcome, string notes, DateTime at)
    {
        var followUp = new FollowUp(incident.Id, outcome, notes);
        _db.FollowUps.Add(followUp);
        Set(followUp, nameof(FarmHand.Domain.Entities.FollowUp.RecordedAt), at);
        Backdate(followUp, at);
    }

    private void Audit(Guid actor, string type, Guid related, string details, DateTime at)
    {
        var audit = new AuditEvent(actor.ToString(), type, related, details);
        _db.AuditEvents.Add(audit);
        Set(audit, nameof(AuditEvent.Timestamp), at);
        Backdate(audit, at);
    }

    private void Backdate<T>(T entity, DateTime createdAt) where T : BaseEntity =>
        _db.Entry(entity).Property(nameof(BaseEntity.CreatedAt)).CurrentValue = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);

    private void Set<T>(T entity, string property, DateTime value) where T : class =>
        _db.Entry(entity).Property(property).CurrentValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
