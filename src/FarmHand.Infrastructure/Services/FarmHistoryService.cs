using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;

namespace FarmHand.Infrastructure.Services;

public class FarmHistoryService : IFarmHistoryService
{
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly ILivestockAnimalRepository _livestockAnimalRepository;

    public FarmHistoryService(
        IPoultryObservationRepository observationRepository,
        IIncidentRepository incidentRepository,
        ILivestockAnimalRepository livestockAnimalRepository)
    {
        _observationRepository = observationRepository;
        _incidentRepository = incidentRepository;
        _livestockAnimalRepository = livestockAnimalRepository;
    }

    public async Task<FarmHistoryContext> GetFarmHistoryAsync(Guid farmId, CancellationToken ct = default)
    {
        var allObservations = await _observationRepository.GetAllAsync(ct);
        var observations = allObservations
            .Where(o => o.FarmId == farmId)
            .OrderByDescending(o => o.RecordedAt)
            .Take(10)
            .Select(o => new RecentObservationSummary(
                o.Id,
                o.RecordedAt,
                o.MortalityCount,
                o.FarmerNotes,
                o.ImageUrl))
            .ToList();

        var incidents = (await _incidentRepository.GetByFarmIdAsync(farmId, ct))
            .Take(10)
            .Select(i => new RecentIncidentSummary(
                i.Id,
                i.DetectedAt,
                i.Severity,
                i.IncidentType,
                i.Status.ToString()))
            .ToList();

        var livestock = await _livestockAnimalRepository.GetByFarmIdAsync(farmId, ct);
        var livestockHistory = livestock
            .Select(l => new LivestockHistorySummary(
                l.Id,
                l.Name,
                l.Species,
                l.DiseaseHistory,
                l.RecentGestationDate,
                l.LastVeterinaryVisit,
                l.Vaccinations))
            .ToList();

        return new FarmHistoryContext(observations, incidents, livestockHistory);
    }
}
