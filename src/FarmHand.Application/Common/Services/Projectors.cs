using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;

namespace FarmHand.Application.Common.Services;

/// <summary>Builds observation DTOs with subject names, assessment and linked incident in batched queries.</summary>
public class ObservationProjector
{
    private readonly IUnitOfWork _unitOfWork;

    public ObservationProjector(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ObservationSummaryDto>> SummariesAsync(IReadOnlyList<PoultryObservation> observations, CancellationToken ct)
    {
        if (observations.Count == 0) return [];

        var ids = observations.Select(o => o.Id).ToList();
        var animals = (await _unitOfWork.LivestockAnimals.GetByIdsAsync(observations.Where(o => o.LivestockAnimalId is not null).Select(o => o.LivestockAnimalId!.Value), ct)).ToDictionary(a => a.Id);
        var categories = (await _unitOfWork.LivestockCategories.GetByIdsAsync(observations.Where(o => o.LivestockCategoryId is not null).Select(o => o.LivestockCategoryId!.Value), ct)).ToDictionary(c => c.Id);
        var diagnoses = (await _unitOfWork.AIDiagnoses.GetByObservationIdsAsync(ids, ct)).ToDictionary(d => d.ObservationId);
        var incidents = (await _unitOfWork.Incidents.GetBySourceObservationIdsAsync(ids, ct))
            .GroupBy(i => i.SourceObservationId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Id);

        return observations.Select(o =>
        {
            animals.TryGetValue(o.LivestockAnimalId ?? Guid.Empty, out var animal);
            categories.TryGetValue(o.LivestockCategoryId ?? Guid.Empty, out var category);
            diagnoses.TryGetValue(o.Id, out var diagnosis);
            var incidentId = diagnosis?.IncidentId ?? (incidents.TryGetValue(o.Id, out var linked) ? linked : null);
            return new ObservationSummaryDto(
                o.Id, o.FarmId, o.LivestockAnimalId, o.LivestockCategoryId,
                Mappers.SubjectName(animal, category),
                o.RecordedAt, o.MortalityCount, o.FarmerNotes, o.ImageUrl, o.ConfirmationStatus,
                o.ExtractedByAi, o.InputChannels, diagnosis?.Severity, incidentId);
        }).ToList();
    }

    public async Task<ObservationDetailDto> DetailAsync(PoultryObservation o, bool reused, CancellationToken ct)
    {
        var animal = o.LivestockAnimalId is { } animalId ? await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct) : null;
        var category = o.LivestockCategoryId is { } categoryId ? await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct) : null;
        var diagnosis = await _unitOfWork.AIDiagnoses.GetByObservationIdAsync(o.Id, ct);
        var incidentId = diagnosis?.IncidentId
            ?? (await _unitOfWork.Incidents.GetBySourceObservationIdsAsync([o.Id], ct)).FirstOrDefault()?.Id;
        var extraction = Mappers.ReadExtraction(o.ExtractionDetails);

        return new ObservationDetailDto(
            o.Id, o.FarmId, o.LivestockAnimalId, o.LivestockCategoryId,
            Mappers.SubjectName(animal, category),
            animal?.Species ?? category?.Species,
            o.RecordedAt, o.MortalityCount, o.WaterConsumption, o.FeedConsumption, o.EggProduction,
            o.FarmerNotes, o.RawTranscript, o.ImageUrl, Mappers.ReadVision(o.VisionAnalysis),
            o.ConfirmationStatus, o.ExtractedByAi, Mappers.IsAiSource(o.ExtractedByAi),
            extraction.Symptoms, extraction.MissingInformation, o.InputChannels,
            diagnosis?.ToDto(), incidentId, reused);
    }
}

/// <summary>
/// Builds vet-request DTOs for the current caller. Farmer contact details are included only for the
/// farm owner, administrators and the assigned vet; <see cref="VetRequestDetailDto.AvailableActions"/>
/// lists exactly the lifecycle actions the backend will accept from this caller.
/// </summary>
public class VetRequestProjector
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IResourceAuthorizationService _authorization;

    public VetRequestProjector(IUnitOfWork unitOfWork, ICurrentUserService currentUser, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<VetRequestDetailDto>> ProjectAsync(IReadOnlyList<VetRequestRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var currentVet = await _authorization.GetCurrentVetAsync(ct);
        var vets = (await _unitOfWork.VetPartners.GetByIdsAsync(rows.Where(r => r.Request.VetPartnerId is not null).Select(r => r.Request.VetPartnerId!.Value), ct)).ToDictionary(v => v.Id);
        var payments = (await _unitOfWork.Payments.GetByVetRequestIdsAsync(rows.Select(r => r.Request.Id), ct))
            .GroupBy(p => p.VetRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).ToList());
        var animals = (await _unitOfWork.LivestockAnimals.GetByIdsAsync(rows.Where(r => r.Incident.LivestockAnimalId is not null).Select(r => r.Incident.LivestockAnimalId!.Value), ct)).ToDictionary(a => a.Id);
        var categories = (await _unitOfWork.LivestockCategories.GetByIdsAsync(rows.Where(r => r.Incident.LivestockCategoryId is not null).Select(r => r.Incident.LivestockCategoryId!.Value), ct)).ToDictionary(c => c.Id);
        var ownerIds = rows.Select(r => Guid.TryParse(r.Farm.OwnerId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty);
        var owners = (await _unitOfWork.Users.GetByIdsAsync(ownerIds, ct)).ToDictionary(u => u.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = new List<VetRequestDetailDto>(rows.Count);
        foreach (var row in rows)
        {
            var r = row.Request;
            vets.TryGetValue(r.VetPartnerId ?? Guid.Empty, out var vet);
            payments.TryGetValue(r.Id, out var requestPayments);
            animals.TryGetValue(row.Incident.LivestockAnimalId ?? Guid.Empty, out var animal);
            categories.TryGetValue(row.Incident.LivestockCategoryId ?? Guid.Empty, out var category);
            owners.TryGetValue(row.Farm.OwnerId, out var owner);

            var isAdmin = _currentUser.IsAdmin;
            var isOwner = _currentUser.UserId is { } uid && string.Equals(row.Farm.OwnerId, uid.ToString(), StringComparison.OrdinalIgnoreCase);
            var isAssignedVet = currentVet is not null && r.VetPartnerId == currentVet.Id && r.Status is RequestStatus.Responded or RequestStatus.Completed;
            var canSeeContact = isAdmin || isOwner || isAssignedVet;

            var latest = requestPayments?.FirstOrDefault();
            var paid = requestPayments?.Any(p => p.Status == PaymentStatus.Completed) == true;
            var paymentStatus = r.FarmerCost.Amount <= 0 ? "NotRequired"
                : paid ? "Paid"
                : latest?.Status == PaymentStatus.Pending ? "Pending"
                : latest?.Status == PaymentStatus.Failed ? "Failed"
                : "Unpaid";

            var actions = new List<string>();
            if ((isOwner || isAdmin) && r.IsActive && !paid) actions.Add("cancel");
            if ((isOwner || isAdmin) && r.Status == RequestStatus.Responded && r.FarmerCost.Amount > 0 && !paid) actions.Add("pay");
            if (currentVet is not null && r.Status == RequestStatus.Pending && (r.VetPartnerId is null || r.VetPartnerId == currentVet.Id))
            {
                actions.Add("accept");
                if (r.VetPartnerId == currentVet.Id) actions.Add("decline");
            }
            if ((isAssignedVet || isAdmin) && r.Status == RequestStatus.Responded)
            {
                actions.Add("treatment_plan");
                if (r.FarmerCost.Amount <= 0 || paid) actions.Add("complete");
            }

            ParticipantDto? farmer = canSeeContact && owner is not null
                ? new ParticipantDto(owner.Id, owner.FullName ?? owner.Email, owner.Email, owner.PhoneNumber)
                : null;

            result.Add(new VetRequestDetailDto(
                r.Id, r.IncidentId, r.RequestType, r.Status, r.FarmerCost.Amount, r.FarmerCost.Currency,
                r.Description, r.PreferredVisitDate, r.CreatedAt, r.RespondedAt, r.CompletedAt, r.CompletionNotes,
                r.CancelledAt, r.DeclinedAt, r.DeclineReason,
                vet?.ToSummary(), r.VetPartnerId is not null && r.Status == RequestStatus.Pending,
                row.Incident.IncidentType, row.Incident.Severity, row.Incident.Status,
                Mappers.SubjectName(animal, category), animal?.Species ?? category?.Species,
                row.Farm.DisplayName, row.Farm.Location, farmer, paymentStatus,
                latest?.ToDto(includePayout: isAdmin || isAssignedVet, row.Farm.DisplayName, row.Incident.Id),
                actions));
        }

        return result;
    }
}
