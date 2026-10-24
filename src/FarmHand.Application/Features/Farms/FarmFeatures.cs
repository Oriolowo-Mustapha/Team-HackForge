using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Services;

namespace FarmHand.Application.Features.Farms;

/// <summary>Farms owned by the caller with headline counts.</summary>
public record GetMyFarmsQuery : IRequest<IReadOnlyList<FarmSummaryDto>>;

public class GetMyFarmsQueryHandler : IRequestHandler<GetMyFarmsQuery, IReadOnlyList<FarmSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public GetMyFarmsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FarmSummaryDto>> Handle(GetMyFarmsQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return [];
        var farms = await _unitOfWork.Farms.ListByOwnerIdAsync(userId.ToString(), ct);
        var counts = await _unitOfWork.Farms.GetRecordCountsAsync(farms.Select(f => f.Id).ToList(), DateTime.UtcNow.AddDays(-30), ct);
        var result = new List<FarmSummaryDto>(farms.Count);
        foreach (var farm in farms)
            result.Add(FarmSummaries.Build(farm, counts[farm.Id]));
        return result;
    }
}

internal static class FarmSummaries
{
    public static async Task<FarmSummaryDto> BuildAsync(Farm farm, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var counts = await unitOfWork.Farms.GetRecordCountsAsync([farm.Id], DateTime.UtcNow.AddDays(-30), ct);
        return Build(farm, counts[farm.Id]);
    }

    public static FarmSummaryDto Build(Farm farm, FarmRecordCounts counts)
    {
        return new FarmSummaryDto(
            farm.Id,
            farm.DisplayName,
            farm.Location,
            farm.CreatedAt,
            counts.Groups,
            counts.Animals,
            counts.Headcount,
            counts.OpenIncidents,
            counts.PendingObservations,
            farm.Latitude,
            farm.Longitude,
            farm.CityOrTown,
            farm.State,
            farm.Country,
            farm.LocationSource);
    }
}

public record GetFarmQuery(Guid FarmId) : IRequest<FarmSummaryDto>;

public class GetFarmQueryHandler : IRequestHandler<GetFarmQuery, FarmSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public GetFarmQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<FarmSummaryDto> Handle(GetFarmQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var farm = await _unitOfWork.Farms.GetByIdAsync(request.FarmId, ct) ?? throw PublicException.NotFound("Farm not found.");
        return await FarmSummaries.BuildAsync(farm, _unitOfWork, ct);
    }
}

public record UpdateFarmCommand(
    Guid FarmId,
    string DisplayName,
    string Location,
    double? Latitude = null,
    double? Longitude = null,
    string? CityOrTown = null,
    string? State = null,
    string? Country = null,
    string? LocationSource = null) : IRequest<FarmSummaryDto>;

public class UpdateFarmCommandValidator : AbstractValidator<UpdateFarmCommand>
{
    public UpdateFarmCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue).WithMessage("Provide both latitude and longitude, or neither.");
        RuleFor(x => x.CityOrTown).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.LocationSource).MaximumLength(50);
    }
}

public class UpdateFarmCommandHandler : IRequestHandler<UpdateFarmCommand, FarmSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public UpdateFarmCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<FarmSummaryDto> Handle(UpdateFarmCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var farm = await _unitOfWork.Farms.GetByIdAsync(request.FarmId, ct) ?? throw PublicException.NotFound("Farm not found.");
        farm.UpdateDetails(
            request.DisplayName.Trim(),
            request.Location.Trim(),
            request.Latitude,
            request.Longitude,
            request.CityOrTown?.Trim(),
            request.State?.Trim(),
            request.Country?.Trim(),
            request.LocationSource?.Trim());
        await _unitOfWork.SaveChangesAsync(ct);
        return await FarmSummaries.BuildAsync(farm, _unitOfWork, ct);
    }
}

/// <summary>
/// Farmer home dashboard: how the farm is doing, which issues need attention, and the single most
/// useful next action. Every number is computed from the farm's own records.
/// </summary>
public record GetFarmOverviewQuery(Guid FarmId) : IRequest<FarmDashboardDto>;

public class GetFarmOverviewQueryHandler : IRequestHandler<GetFarmOverviewQuery, FarmDashboardDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ObservationProjector _observations;
    private readonly VetRequestProjector _requests;

    public GetFarmOverviewQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ObservationProjector observations, VetRequestProjector requests)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _observations = observations;
        _requests = requests;
    }

    public async Task<FarmDashboardDto> Handle(GetFarmOverviewQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var farm = await _unitOfWork.Farms.GetByIdAsync(request.FarmId, ct) ?? throw PublicException.NotFound("Farm not found.");
        var now = DateTime.UtcNow;

        var recordCounts = (await _unitOfWork.Farms.GetRecordCountsAsync([farm.Id], now.AddDays(-30), ct))[farm.Id];
        var open = (await _unitOfWork.Incidents.GetOpenByFarmIdAsync(farm.Id, ct)).ToList();
        var recentIncidents = await _unitOfWork.Incidents.SearchAsync(
            new IncidentSearch([farm.Id], null, null, null, false, null, null, now.AddDays(-30), null, null, new Paging(1, 8), NewestFirst: true), ct);
        var recentResolved = await _unitOfWork.Incidents.SearchAsync(
            new IncidentSearch([farm.Id], null, IncidentStatus.Resolved, null, false, null, null, null, null, null, new Paging(1, 8), ResolvedSince: now.AddDays(-30)), ct);
        var recentObservations = await _unitOfWork.PoultryObservations.GetByFarmSinceAsync(farm.Id, now.Date.AddDays(-13), ct);

        var pendingPage = await _unitOfWork.PoultryObservations.SearchAsync(
            new ObservationSearch([farm.Id], ObservationConfirmationStatus.Pending, null, null, new Paging(1, 5)), ct);
        var pending = await _observations.SummariesAsync(pendingPage.Items, ct);

        var requestRows = (await _unitOfWork.VetRequests.SearchAsync(
            new VetRequestSearch([farm.Id], null, null, null, null, new Paging(1, 50)), ct)).Items;
        var activeRows = (await _unitOfWork.VetRequests.GetActiveByFarmAsync(farm.Id, ct)).ToList();
        var activeRequests = await _requests.ProjectAsync(activeRows.Take(5).ToList(), ct);
        var paidRequestIds = await _unitOfWork.Payments.GetPaidRequestIdsAsync(activeRows.Select(r => r.Request.Id), ct);

        // Reminders: incident follow-up dates and incomplete treatment steps.
        var reminders = open
            .Where(i => i.FollowUpDate is not null)
            .Select(i => new FollowUpReminderDto(i.Id, $"Follow up: {i.IncidentType}", i.FollowUpDate!.Value, "follow_up", i.FollowUpDate < now))
            .ToList();
        var steps = await _unitOfWork.TreatmentPlans.GetPendingByFarmAsync(farm.Id, 6, ct);
        reminders.AddRange(steps.Select(s =>
            new FollowUpReminderDto(s.IncidentId, s.Action, s.DueDate, "treatment", s.DueDate < now)));
        reminders = reminders.OrderBy(r => r.DueAt).Take(6).ToList();

        // Recent activity from real records.
        var activity = new List<ActivityDto>();
        activity.AddRange(recentObservations.Take(6).Select(o => new ActivityDto(o.RecordedAt, "observation",
            o.IsPending ? "Report waiting for review" : "Health report confirmed",
            o.FarmerNotes, o.Id, null)));
        activity.AddRange(recentIncidents.Items.Select(i => new ActivityDto(i.DetectedAt, "incident", $"Case opened: {i.IncidentType}", i.EvidenceSummary, i.Id, i.Severity)));
        activity.AddRange(recentResolved.Items.Where(i => i.ResolvedAt >= now.AddDays(-30)).Select(i => new ActivityDto(i.ResolvedAt!.Value, "resolved", $"Case resolved: {i.IncidentType}", null, i.Id, i.Severity)));
        activity.AddRange(requestRows.Where(r => r.Request.RespondedAt is not null).Select(r => new ActivityDto(r.Request.RespondedAt!.Value, "vet", "A vet accepted your request", null, r.Request.IncidentId, null)));

        var days = Enumerable.Range(0, 14).Select(offset => DateOnly.FromDateTime(now.Date.AddDays(-13 + offset))).ToList();
        var byDay = recentObservations.GroupBy(o => DateOnly.FromDateTime(o.RecordedAt)).ToDictionary(g => g.Key, g => (Count: g.Count(), Deaths: g.Sum(o => o.MortalityCount)));
        var last14 = days.Select(d => byDay.TryGetValue(d, out var v) ? new DailyCountDto(d, v.Count, v.Deaths) : new DailyCountDto(d, 0, 0)).ToList();

        var counts = new DashboardCountsDto(
            recordCounts.Animals,
            recordCounts.Headcount,
            recordCounts.Groups,
            open.Count,
            open.Count(i => i.Severity >= Severity.High),
            pendingPage.TotalCount,
            activeRows.Count,
            recentObservations.Count(o => o.RecordedAt >= now.AddDays(-7)),
            recordCounts.RecentlyResolved);

        var (nextAction, nextKind) = ChooseNextAction(counts, open, activeRows, paidRequestIds, pending, reminders, recordCounts.Groups, recordCounts.Incidents, recentObservations.Count);

        return new FarmDashboardDto(
            new FarmDto(farm.Id, farm.OwnerId, farm.DisplayName, farm.Location, farm.CreatedAt,
                farm.Latitude, farm.Longitude, farm.CityOrTown, farm.State, farm.Country, farm.LocationSource),
            counts,
            open.OrderByDescending(i => i.Severity).ThenByDescending(i => i.DetectedAt).Take(5).Select(i => i.ToDto()).ToList(),
            pending,
            activeRequests,
            reminders,
            activity.OrderByDescending(a => a.At).Take(8).ToList(),
            Enum.GetValues<Severity>().Select(s => new SeverityCountDto(s, open.Count(i => i.Severity == s))).ToList(),
            last14,
            nextAction,
            nextKind);
    }

    private static (string, string) ChooseNextAction(
        DashboardCountsDto counts,
        List<Incident> open,
        List<VetRequestRow> activeRows,
        IReadOnlySet<Guid> paidRequestIds,
        IReadOnlyList<ObservationSummaryDto> pending,
        List<FollowUpReminderDto> reminders,
        int groups,
        int incidentTotal,
        int observationsRecent)
    {
        if (groups == 0 && counts.Animals == 0)
            return ("Add your first flock or animal so reports can be linked to them.", "add_livestock");

        var urgentWithoutVet = open.FirstOrDefault(i => i.Severity >= Severity.High && activeRows.All(r => r.Request.IncidentId != i.Id));
        if (urgentWithoutVet is not null)
            return ($"\"{urgentWithoutVet.IncidentType}\" is {urgentWithoutVet.Severity.ToString().ToLowerInvariant()} severity. Ask a vet to review it.", "request_vet");

        if (pending.Count > 0)
            return ("You have a health report waiting for your review.", "review_observation");

        var unpaid = activeRows.FirstOrDefault(r => r.Request.Status == RequestStatus.Responded
            && r.Request.FarmerCost.Amount > 0 && !paidRequestIds.Contains(r.Request.Id));
        if (unpaid is not null)
            return ("A vet accepted your request. Review the fee and pay to confirm the visit.", "pay");

        if (reminders.Any(r => r.IsOverdue))
            return ("A follow-up is overdue. Record how the animals are doing.", "follow_up");

        if (incidentTotal == 0 && observationsRecent == 0)
            return ("Record your first health check — a quick note, voice message or photo.", "first_report");

        return ("Everything is up to date. Record today's check when you walk the farm.", "routine_check");
    }
}
