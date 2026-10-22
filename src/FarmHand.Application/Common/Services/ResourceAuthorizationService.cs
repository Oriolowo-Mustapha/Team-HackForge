using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;

namespace FarmHand.Application.Common.Services;

public class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ResourceAuthorizationService(ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
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
        if (!await CanAccessFarmAsync(farmId, ct))
            throw new UnauthorizedAccessException("You do not have access to this farm.");
    }

    public async Task EnsureIncidentAccessAsync(Guid incidentId, CancellationToken ct = default)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(incidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found.");

        if (_currentUser.IsVet) return;
        await EnsureFarmAccessAsync(incident.FarmId, ct);
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

        if (_currentUser.IsAdmin || _currentUser.IsVet) return;

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
