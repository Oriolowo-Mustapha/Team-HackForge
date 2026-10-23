using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;

namespace FarmHand.Application.Common.Services;

public class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private VetPartner? _cachedVet;
    private bool _vetLoaded;

    public ResourceAuthorizationService(ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<VetPartner?> GetCurrentVetAsync(CancellationToken ct = default)
    {
        if (_vetLoaded) return _cachedVet;
        _vetLoaded = true;
        if (!_currentUser.IsVet || _currentUser.UserId is null) return null;

        var vet = await _unitOfWork.VetPartners.GetByUserIdAsync(_currentUser.UserId.Value, ct);
        _cachedVet = vet is { IsActive: true } ? vet : null;
        return _cachedVet;
    }

    public async Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken ct = default)
    {
        if (_currentUser.IsAdmin) return true;
        if (_currentUser.UserId is null) return false;

        var farm = await _unitOfWork.Farms.GetByIdAsync(farmId, ct);
        return farm is not null
            && string.Equals(farm.OwnerId, _currentUser.UserId.Value.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public async Task EnsureFarmAccessAsync(Guid farmId, CancellationToken ct = default)
    {
        var farm = await _unitOfWork.Farms.GetByIdAsync(farmId, ct)
            ?? throw new KeyNotFoundException($"Farm {farmId} not found.");

        if (_currentUser.IsAdmin) return;
        if (_currentUser.UserId is { } userId
            && string.Equals(farm.OwnerId, userId.ToString(), StringComparison.OrdinalIgnoreCase))
            return;

        throw new UnauthorizedAccessException("You do not have access to this farm.");
    }

    public async Task<IReadOnlyList<Guid>?> GetAccessibleFarmIdsAsync(CancellationToken ct = default)
    {
        if (_currentUser.IsAdmin) return null;
        if (_currentUser.UserId is not { } userId) return [];
        var farms = await _unitOfWork.Farms.ListByOwnerIdAsync(userId.ToString(), ct);
        return farms.Select(f => f.Id).ToList();
    }

    public async Task<IncidentAccess> GetIncidentAccessAsync(Guid incidentId, CancellationToken ct = default)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found.");

        if (_currentUser.IsAdmin) return IncidentAccess.Admin;

        if (_currentUser.IsVet)
        {
            var vet = await GetCurrentVetAsync(ct);
            if (vet is null) return IncidentAccess.None;

            var requests = await _unitOfWork.VetRequests.GetByIncidentIdAsync(incidentId, ct);
            if (requests.Any(r => r.VetPartnerId == vet.Id && r.Status is RequestStatus.Responded or RequestStatus.Completed))
                return IncidentAccess.AssignedVet;
            if (requests.Any(r => r.Status == RequestStatus.Pending && (r.VetPartnerId == null || r.VetPartnerId == vet.Id)))
                return IncidentAccess.ProspectiveVet;
            return IncidentAccess.None;
        }

        return await CanAccessFarmAsync(incident.FarmId, ct) ? IncidentAccess.Owner : IncidentAccess.None;
    }

    public async Task EnsureIncidentAccessAsync(Guid incidentId, CancellationToken ct = default)
    {
        var access = await GetIncidentAccessAsync(incidentId, ct);
        if (access is IncidentAccess.None or IncidentAccess.ProspectiveVet)
            throw new UnauthorizedAccessException("You do not have access to this case.");
    }

    public async Task EnsureIncidentWriteAccessAsync(Guid incidentId, CancellationToken ct = default)
    {
        var access = await GetIncidentAccessAsync(incidentId, ct);
        if (access is IncidentAccess.None or IncidentAccess.ProspectiveVet)
            throw new UnauthorizedAccessException("You cannot change this case.");
    }

    public async Task EnsureObservationAccessAsync(Guid observationId, CancellationToken ct = default)
    {
        var observation = await _unitOfWork.PoultryObservations.GetByIdAsync(observationId, ct)
            ?? throw new KeyNotFoundException($"Observation {observationId} not found.");
        await EnsureFarmAccessAsync(observation.FarmId, ct);
    }

    public async Task EnsureVetRequestAccessAsync(Guid vetRequestId, CancellationToken ct = default)
    {
        var vetRequest = await _unitOfWork.VetRequests.GetByIdAsync(vetRequestId, ct)
            ?? throw new KeyNotFoundException($"Vet request {vetRequestId} not found.");

        if (_currentUser.IsAdmin) return;

        if (_currentUser.IsVet)
        {
            var vet = await GetCurrentVetAsync(ct);
            if (vet is not null)
            {
                if (vetRequest.VetPartnerId == vet.Id) return;
                if (vetRequest.Status == RequestStatus.Pending && vetRequest.VetPartnerId is null) return;
            }
            throw new UnauthorizedAccessException("You can only access vet requests assigned or open to you.");
        }

        var incident = await _unitOfWork.Incidents.GetByIdAsync(vetRequest.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {vetRequest.IncidentId} not found.");
        await EnsureFarmAccessAsync(incident.FarmId, ct);
    }

    public async Task EnsureAnimalAccessAsync(Guid animalId, CancellationToken ct = default)
    {
        var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct)
            ?? throw new KeyNotFoundException($"Animal {animalId} not found.");
        await EnsureFarmAccessAsync(animal.FarmId, ct);
    }

    public async Task EnsureCategoryAccessAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct)
            ?? throw new KeyNotFoundException($"Category {categoryId} not found.");
        await EnsureFarmAccessAsync(category.FarmId, ct);
    }

    public void EnsureVetOrAdmin()
    {
        if (!_currentUser.IsAdmin && !_currentUser.IsVet)
            throw new UnauthorizedAccessException("Only veterinarians can perform this action.");
    }
}
