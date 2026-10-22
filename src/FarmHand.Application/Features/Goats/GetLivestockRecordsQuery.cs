using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Goats;

/// <summary>List livestock records for a farm.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
public record GetLivestockRecordsQuery(Guid FarmId) : IRequest<List<LivestockRecordDto>>;

public class GetLivestockRecordsQueryHandler : IRequestHandler<GetLivestockRecordsQuery, List<LivestockRecordDto>>
{
    private readonly ILivestockAnimalRepository _livestockRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetLivestockRecordsQueryHandler(
        ILivestockAnimalRepository livestockRepository,
        IResourceAuthorizationService authorization)
    {
        _livestockRepository = livestockRepository;
        _authorization = authorization;
    }

    public async Task<List<LivestockRecordDto>> Handle(GetLivestockRecordsQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var animals = await _livestockRepository.GetByFarmIdAsync(request.FarmId, ct);

        return animals.Select(a => new LivestockRecordDto(
            a.Id,
            a.FarmId,
            a.CategoryId,
            a.Name,
            a.Species,
            a.Breed,
            a.AgeMonths,
            a.Gender,
            a.IsActive,
            a.CreatedAt,
            a.DiseaseHistory,
            a.RecentGestationDate,
            a.LastVeterinaryVisit,
            a.Vaccinations,
            a.LastLactationDate,
            a.Parentage
        )).ToList();
    }
}

public class GetLivestockRecordsQueryValidator : AbstractValidator<GetLivestockRecordsQuery>
{
    public GetLivestockRecordsQueryValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
    }
}
