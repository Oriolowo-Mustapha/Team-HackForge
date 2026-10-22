using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Incidents;

/// <summary>Creates an incident. DetectedAt is assigned by the server.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
/// <param name="LivestockAnimalId">Affected animal id, when animal-level.</param>
/// <param name="LivestockCategoryId">Affected category id, when flock-level.</param>
/// <param name="IncidentType">Short incident classification.</param>
/// <param name="Severity">Severity level, sent as an integer (0-3).</param>
/// <param name="TriggeringObservations">Evidence that triggered the incident.</param>
/// <param name="EvidenceSummary">Summary shown to the farmer and vet.</param>
/// <param name="RuleVersion">Optional detection rule version.</param>
public record CreateIncidentCommand(
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string IncidentType,
    Domain.Enums.Severity Severity,
    string TriggeringObservations,
    string EvidenceSummary,
    string? RuleVersion = null) : IRequest<Guid>;

public class CreateIncidentCommandHandler : IRequestHandler<CreateIncidentCommand, Guid>
{
    private readonly IIncidentRepository _incidentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateIncidentCommandHandler(
        IIncidentRepository incidentRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _incidentRepository = incidentRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateIncidentCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        if (request.LivestockAnimalId is { } animalId)
        {
            var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct)
                ?? throw new KeyNotFoundException($"Animal {animalId} not found.");
            if (animal.FarmId != request.FarmId)
                throw new ArgumentException("The selected animal does not belong to this farm.");
        }

        if (request.LivestockCategoryId is { } categoryId)
        {
            var category = await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct)
                ?? throw new KeyNotFoundException($"Category {categoryId} not found.");
            if (category.FarmId != request.FarmId)
                throw new ArgumentException("The selected category does not belong to this farm.");
        }

        var incident = new Incident(
            request.FarmId,
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            DateTime.UtcNow,
            request.IncidentType,
            request.Severity,
            request.TriggeringObservations,
            request.EvidenceSummary,
            request.RuleVersion);

        await _incidentRepository.AddAsync(incident, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return incident.Id;
    }
}

public class CreateIncidentCommandValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x)
            .Must(x => x.LivestockAnimalId is not null || x.LivestockCategoryId is not null)
            .WithMessage("Provide either a livestock animal or a category.");
        RuleFor(x => x.IncidentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceSummary).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.TriggeringObservations).NotEmpty().MaximumLength(1000);
    }
}
