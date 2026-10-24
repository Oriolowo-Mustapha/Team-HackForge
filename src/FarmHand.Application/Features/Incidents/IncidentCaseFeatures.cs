using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Services;

namespace FarmHand.Application.Features.Incidents;

/// <summary>
/// Searches health cases visible to the caller: a farmer's own farms, a vet's assigned cases, or every
/// case for administrators. Results are ordered by severity, then most recent.
/// </summary>
public record SearchIncidentsQuery(
    Guid? FarmId = null,
    IncidentStatus? Status = null,
    Severity? Severity = null,
    bool OpenOnly = false,
    Guid? AnimalId = null,
    Guid? CategoryId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Q = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<IncidentListItemDto>>;

public class SearchIncidentsQueryValidator : AbstractValidator<SearchIncidentsQuery>
{
    public SearchIncidentsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Severity).IsInEnum().When(x => x.Severity is not null);
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class SearchIncidentsQueryHandler : IRequestHandler<SearchIncidentsQuery, PagedResult<IncidentListItemDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public SearchIncidentsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<IncidentListItemDto>> Handle(SearchIncidentsQuery request, CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? farmIds = null;
        Guid? assignedVetId = null;
        var paging = new Paging(request.Page, request.PageSize);

        if (_currentUser.IsVet)
        {
            var vet = await _authorization.GetCurrentVetAsync(ct);
            if (vet is null) return Empty(paging);
            assignedVetId = vet.Id;
        }
        else if (request.FarmId is { } farmId)
        {
            await _authorization.EnsureFarmAccessAsync(farmId, ct);
            farmIds = [farmId];
        }
        else
        {
            farmIds = await _authorization.GetAccessibleFarmIdsAsync(ct);
        }

        if (_currentUser.IsVet && request.FarmId is { } vetFarm)
            farmIds = [vetFarm];

        var page = await _unitOfWork.Incidents.SearchAsync(new IncidentSearch(
            farmIds, null, request.Status, request.Severity, request.OpenOnly,
            request.AnimalId, request.CategoryId, request.From, request.To, request.Q, paging, assignedVetId), ct);

        var incidents = page.Items;
        var farms = (await _unitOfWork.Farms.GetByIdsAsync(incidents.Select(i => i.FarmId), ct)).ToDictionary(f => f.Id);
        var animals = (await _unitOfWork.LivestockAnimals.GetByIdsAsync(incidents.Where(i => i.LivestockAnimalId is not null).Select(i => i.LivestockAnimalId!.Value), ct)).ToDictionary(a => a.Id);
        var categories = (await _unitOfWork.LivestockCategories.GetByIdsAsync(incidents.Where(i => i.LivestockCategoryId is not null).Select(i => i.LivestockCategoryId!.Value), ct)).ToDictionary(c => c.Id);

        var latestRequests = (await _unitOfWork.VetRequests.GetByIncidentIdsAsync(incidents.Select(i => i.Id), ct))
            .GroupBy(r => r.IncidentId)
            .ToDictionary(g => g.Key, g => g.First());
        var vets = (await _unitOfWork.VetPartners.GetByIdsAsync(latestRequests.Values.Where(r => r.VetPartnerId is not null).Select(r => r.VetPartnerId!.Value), ct)).ToDictionary(v => v.Id);

        var items = incidents.Select(i =>
        {
            animals.TryGetValue(i.LivestockAnimalId ?? Guid.Empty, out var animal);
            categories.TryGetValue(i.LivestockCategoryId ?? Guid.Empty, out var category);
            latestRequests.TryGetValue(i.Id, out var latest);
            VetPartner? vet = null;
            if (latest?.VetPartnerId is { } vetId) vets.TryGetValue(vetId, out vet);
            return new IncidentListItemDto(
                i.ToDto(),
                farms.TryGetValue(i.FarmId, out var farm) ? farm.DisplayName : "Farm",
                Mappers.SubjectName(animal, category),
                animal?.Species ?? category?.Species,
                animal is null,
                latest?.Status,
                vet?.Name);
        }).ToList();

        return new PagedResult<IncidentListItemDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }

    private static PagedResult<IncidentListItemDto> Empty(Paging paging) => new([], paging.Page, paging.PageSize, 0, 0);
}

/// <summary>Full case file for one incident: subject, source report, vet coordination, plans, follow-ups and timeline.</summary>
public record GetIncidentCaseQuery(Guid IncidentId) : IRequest<IncidentCaseDto>;

public class GetIncidentCaseQueryHandler : IRequestHandler<GetIncidentCaseQuery, IncidentCaseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ObservationProjector _observations;
    private readonly VetRequestProjector _requests;

    public GetIncidentCaseQueryHandler(
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ObservationProjector observations,
        VetRequestProjector requests)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _observations = observations;
        _requests = requests;
    }

    public async Task<IncidentCaseDto> Handle(GetIncidentCaseQuery request, CancellationToken ct)
    {
        var access = await _authorization.GetIncidentAccessAsync(request.IncidentId, ct);
        if (access is IncidentAccess.None or IncidentAccess.ProspectiveVet)
            throw new UnauthorizedAccessException("You do not have access to this case.");

        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw PublicException.NotFound("Case not found.");
        var farm = await _unitOfWork.Farms.GetByIdAsync(incident.FarmId, ct)
            ?? throw PublicException.NotFound("Farm not found.");

        var animal = incident.LivestockAnimalId is { } animalId ? await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct) : null;
        var category = incident.LivestockCategoryId is { } categoryId ? await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct) : null;
        var owner = Guid.TryParse(farm.OwnerId, out var ownerId) ? await _unitOfWork.Users.GetByIdAsync(ownerId, ct) : null;

        ObservationDetailDto? source = null;
        if (incident.SourceObservationId is { } observationId
            && await _unitOfWork.PoultryObservations.GetByIdAsync(observationId, ct) is { } observation
            && observation.FarmId == incident.FarmId)
            source = await _observations.DetailAsync(observation, reused: false, ct);

        var currentVet = await _authorization.GetCurrentVetAsync(ct);
        var rows = (await _unitOfWork.VetRequests.SearchAsync(new VetRequestSearch(null, null, null, null, incident.Id, new Paging(1, 50)), ct)).Items
            .Where(r => access != IncidentAccess.AssignedVet || r.Request.VetPartnerId == currentVet?.Id)
            .ToList();
        var requests = await _requests.ProjectAsync(rows, ct);

        var plans = await _unitOfWork.TreatmentPlans.GetByVetRequestIdsAsync(rows.Select(r => r.Request.Id), ct);
        var schedule = (await _unitOfWork.TreatmentPlans.GetScheduleItemsAsync(plans.Select(p => p.Id), ct))
            .GroupBy(s => s.TreatmentPlanId).ToDictionary(g => g.Key, g => g.ToList());
        var authorIds = plans.Select(p => Guid.TryParse(p.AuthoredByVetId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty);
        var authors = (await _unitOfWork.VetPartners.GetByIdsAsync(authorIds, ct)).ToDictionary(v => v.Id.ToString());
        var planDtos = plans.Select(p => new TreatmentPlanDetailDto(
            p.Id, p.VetRequestId, p.AuthoredByVetId,
            authors.TryGetValue(p.AuthoredByVetId, out var author) ? author.Name : null,
            p.InstructionsText, p.Language, p.CreatedAt,
            (schedule.TryGetValue(p.Id, out var items) ? items : [])
                .Select(s => new TreatmentScheduleItemDto(s.Id, s.Action, s.DueDate, s.IsCompleted, s.CompletedAt)).ToList())).ToList();

        var prevention = await _unitOfWork.PreventionPlans.GetByIncidentIdAsync(incident.Id, ct);
        var followUps = (await _unitOfWork.FollowUps.GetByIncidentIdAsync(incident.Id, ct))
            .OrderByDescending(f => f.RecordedAt)
            .Select(f => new FollowUpDto(f.Id, f.IncidentId, f.FarmerResponse, f.Notes, f.RecordedAt)).ToList();
        var actions = (await _unitOfWork.IncidentActions.GetByIncidentIdAsync(incident.Id, ct))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new IncidentActionDto(a.Id, a.IncidentId, a.Description, a.Priority, a.Status, a.CompletedAt, a.CompletionNotes)).ToList();

        var audit = await _unitOfWork.AuditEvents.SearchAsync(new AuditEventSearch(null, null, incident.Id, new Paging(1, 50)), ct);
        var actorIds = audit.Items.Select(a => Guid.TryParse(a.ActorId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty);
        var actors = (await _unitOfWork.Users.GetByIdsAsync(actorIds, ct)).ToDictionary(u => u.Id.ToString());

        var timeline = new List<TimelineEntryDto>
        {
            new(incident.DetectedAt, "opened", "Case opened", $"{incident.IncidentType} · {incident.Severity} severity", null)
        };
        if (source is not null)
            timeline.Add(new(source.RecordedAt, "report", "Health report recorded", source.FarmerNotes, owner?.FullName));
        timeline.AddRange(audit.Items.Where(a => a.EventType == "incident.status_changed").Select(a => new TimelineEntryDto(
            a.Timestamp, "status", "Status changed", a.Details, actors.TryGetValue(a.ActorId, out var actor) ? actor.FullName ?? actor.Email : null)));
        foreach (var r in requests)
        {
            timeline.Add(new(r.CreatedAt, "vet_request", "Veterinary help requested", r.Vet is null ? "Open to all available vets" : $"Sent to {r.Vet.Name}", null));
            if (r.RespondedAt is { } responded) timeline.Add(new(responded, "vet_accepted", "Vet accepted the request", r.Vet?.Name, r.Vet?.Name));
            if (r.LatestPayment?.PaidAt is { } paid) timeline.Add(new(paid, "payment", r.LatestPayment.Mode == PaymentMode.Simulated ? "Simulated payment confirmed" : "Payment confirmed", $"{r.LatestPayment.TotalAmount:N2} {r.LatestPayment.Currency}", null));
            if (r.CompletedAt is { } completed) timeline.Add(new(completed, "vet_completed", "Veterinary visit completed", r.CompletionNotes, r.Vet?.Name));
            if (r.CancelledAt is { } cancelled) timeline.Add(new(cancelled, "vet_cancelled", "Vet request cancelled", null, null));
            if (r.DeclinedAt is { } declined) timeline.Add(new(declined, "vet_declined", "Vet declined the request", r.DeclineReason, r.Vet?.Name));
        }
        timeline.AddRange(planDtos.Select(p => new TimelineEntryDto(p.CreatedAt, "treatment", "Treatment plan added", null, p.AuthorName)));
        timeline.AddRange(followUps.Select(f => new TimelineEntryDto(f.RecordedAt, "follow_up", $"Follow-up: {f.FarmerResponse}", f.Notes, null)));
        timeline.AddRange(actions.Where(a => a.CompletedAt is not null).Select(a => new TimelineEntryDto(a.CompletedAt!.Value, "action", $"Done: {a.Description}", a.CompletionNotes, null)));
        if (incident.ResolvedAt is { } resolvedAt)
            timeline.Add(new(resolvedAt, "resolved", "Case resolved", null, null));

        var canWrite = access is IncidentAccess.Owner or IncidentAccess.Admin or IncidentAccess.AssignedVet;
        var canRequestVet = access is IncidentAccess.Owner or IncidentAccess.Admin
            && !incident.IsClosed
            && !rows.Any(r => r.Request.IsActive);

        return new IncidentCaseDto(
            incident.ToDto(),
            farm.DisplayName,
            farm.Location,
            Mappers.SubjectName(animal, category),
            animal?.Species ?? category?.Species,
            animal is null,
            owner is null ? null : new ParticipantDto(owner.Id, owner.FullName ?? owner.Email, owner.Email, owner.PhoneNumber),
            source,
            requests,
            planDtos,
            prevention is null ? null : new PreventionPlanDto(prevention.Id, prevention.IncidentId, prevention.Content, prevention.GeneratedFromHistory),
            followUps,
            actions,
            timeline.OrderBy(t => t.At).ToList(),
            canWrite ? incident.AllowedTransitions : [],
            access.ToString(),
            canRequestVet);
    }
}
