using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Services;

namespace FarmHand.Application.Features.Livestock;

internal static class AnimalMapping
{
    public static async Task<IReadOnlyList<AnimalDto>> ToDtosAsync(IReadOnlyList<LivestockAnimal> animals, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        if (animals.Count == 0) return [];
        var farmIds = animals.Select(a => a.FarmId).Distinct().ToList();
        var categories = (await unitOfWork.LivestockCategories.GetByIdsAsync(animals.Select(a => a.CategoryId), ct)).ToDictionary(c => c.Id);

        var openIncidents = new List<Incident>();
        var lastObserved = new Dictionary<Guid, DateTime>();
        foreach (var farmId in farmIds)
        {
            openIncidents.AddRange(await unitOfWork.Incidents.GetOpenByFarmIdAsync(farmId, ct));
            foreach (var o in await unitOfWork.PoultryObservations.GetRecentByFarmAsync(farmId, 500, ct))
            {
                if (o.LivestockAnimalId is { } aid && (!lastObserved.TryGetValue(aid, out var at) || o.RecordedAt > at))
                    lastObserved[aid] = o.RecordedAt;
            }
        }

        return animals.Select(a =>
        {
            var mine = openIncidents.Where(i => i.LivestockAnimalId == a.Id).ToList();
            return new AnimalDto(
                a.Id, a.FarmId, a.CategoryId, categories.TryGetValue(a.CategoryId, out var c) ? c.Name : null,
                a.Name, a.Species, a.Breed, a.AgeMonths, a.Gender, a.Notes, a.PhotoUrl, a.IsActive, a.CreatedAt,
                a.DiseaseHistory, a.RecentGestationDate, a.LastVeterinaryVisit, a.Vaccinations, a.LastLactationDate, a.Parentage,
                mine.Count, mine.Count == 0 ? null : mine.Max(i => i.Severity),
                lastObserved.TryGetValue(a.Id, out var last) ? last : null);
        }).ToList();
    }
}

/// <summary>Registers an individual animal with a free-text name or tag.</summary>
public record CreateAnimalCommand(
    Guid FarmId,
    Guid CategoryId,
    string Name,
    string Species,
    string? Breed = null,
    int? AgeMonths = null,
    string? Sex = null,
    string? Notes = null,
    string? DiseaseHistory = null,
    DateTime? RecentGestationDate = null,
    DateTime? LastVeterinaryVisit = null,
    string? Vaccinations = null,
    DateTime? LastLactationDate = null,
    string? Parentage = null) : IRequest<AnimalDto>;

public class AnimalFieldsValidator<T> : AbstractValidator<T> where T : CreateAnimalCommand
{
    public AnimalFieldsValidator()
    {
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty).WithMessage("Choose a group for this animal.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Species).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Breed).MaximumLength(100);
        RuleFor(x => x.AgeMonths).InclusiveBetween(0, 600).When(x => x.AgeMonths is not null);
        RuleFor(x => x.Sex).MaximumLength(20);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.DiseaseHistory).MaximumLength(1000);
        RuleFor(x => x.Vaccinations).MaximumLength(500);
        RuleFor(x => x.Parentage).MaximumLength(500);
        RuleFor(x => x.RecentGestationDate).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).When(x => x.RecentGestationDate is not null);
        RuleFor(x => x.LastVeterinaryVisit).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).When(x => x.LastVeterinaryVisit is not null);
        RuleFor(x => x.LastLactationDate).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1)).When(x => x.LastLactationDate is not null);
    }
}

public class CreateAnimalCommandValidator : AnimalFieldsValidator<CreateAnimalCommand>
{
    public CreateAnimalCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
    }
}

public class CreateAnimalCommandHandler : IRequestHandler<CreateAnimalCommand, AnimalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateAnimalCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<AnimalDto> Handle(CreateAnimalCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var category = await _unitOfWork.LivestockCategories.GetByIdAsync(request.CategoryId, ct)
            ?? throw PublicException.NotFound("Group not found.");
        if (category.FarmId != request.FarmId)
            throw PublicException.BadRequest("The selected group does not belong to this farm.");

        var animal = new LivestockAnimal(
            request.FarmId, request.CategoryId, request.Name.Trim(), request.Species.Trim(),
            Trim(request.Breed), request.AgeMonths, Trim(request.Sex), Trim(request.Notes),
            Trim(request.DiseaseHistory), Utc(request.RecentGestationDate), Utc(request.LastVeterinaryVisit),
            Trim(request.Vaccinations), Utc(request.LastLactationDate), Trim(request.Parentage));

        await _unitOfWork.LivestockAnimals.AddAsync(animal, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await AnimalMapping.ToDtosAsync([animal], _unitOfWork, ct))[0];
    }

    internal static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal static DateTime? Utc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}

/// <summary>Updates an animal's details, history and group.</summary>
public record UpdateAnimalCommand(
    Guid AnimalId,
    Guid CategoryId,
    string Name,
    string Species,
    string? Breed = null,
    int? AgeMonths = null,
    string? Sex = null,
    string? Notes = null,
    string? DiseaseHistory = null,
    DateTime? RecentGestationDate = null,
    DateTime? LastVeterinaryVisit = null,
    string? Vaccinations = null,
    DateTime? LastLactationDate = null,
    string? Parentage = null)
    : CreateAnimalCommand(Guid.Empty, CategoryId, Name, Species, Breed, AgeMonths, Sex, Notes, DiseaseHistory, RecentGestationDate, LastVeterinaryVisit, Vaccinations, LastLactationDate, Parentage), IRequest<AnimalDto>;

public class UpdateAnimalCommandValidator : AnimalFieldsValidator<UpdateAnimalCommand>
{
    public UpdateAnimalCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEqual(Guid.Empty);
    }
}

public class UpdateAnimalCommandHandler : IRequestHandler<UpdateAnimalCommand, AnimalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public UpdateAnimalCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<AnimalDto> Handle(UpdateAnimalCommand request, CancellationToken ct)
    {
        var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(request.AnimalId, ct)
            ?? throw PublicException.NotFound("Animal not found.");
        await _authorization.EnsureFarmAccessAsync(animal.FarmId, ct);

        if (request.CategoryId != animal.CategoryId)
        {
            var category = await _unitOfWork.LivestockCategories.GetByIdAsync(request.CategoryId, ct)
                ?? throw PublicException.NotFound("Group not found.");
            if (category.FarmId != animal.FarmId)
                throw PublicException.BadRequest("The selected group does not belong to this farm.");
            animal.MoveToCategory(category.Id);
        }

        animal.UpdateDetails(request.Name.Trim(), request.Species.Trim(), CreateAnimalCommandHandler.Trim(request.Breed),
            request.AgeMonths, CreateAnimalCommandHandler.Trim(request.Sex), CreateAnimalCommandHandler.Trim(request.Notes));
        animal.UpdateHistory(CreateAnimalCommandHandler.Trim(request.DiseaseHistory), CreateAnimalCommandHandler.Utc(request.RecentGestationDate),
            CreateAnimalCommandHandler.Utc(request.LastVeterinaryVisit), CreateAnimalCommandHandler.Trim(request.Vaccinations),
            CreateAnimalCommandHandler.Utc(request.LastLactationDate), CreateAnimalCommandHandler.Trim(request.Parentage));

        await _unitOfWork.SaveChangesAsync(ct);
        return (await AnimalMapping.ToDtosAsync([animal], _unitOfWork, ct))[0];
    }
}

/// <summary>Archives (e.g. sold or deceased) or restores an animal. Archived animals keep their history.</summary>
public record SetAnimalActiveCommand(Guid AnimalId, bool Active) : IRequest<AnimalDto>;

public class SetAnimalActiveCommandHandler : IRequestHandler<SetAnimalActiveCommand, AnimalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public SetAnimalActiveCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<AnimalDto> Handle(SetAnimalActiveCommand request, CancellationToken ct)
    {
        var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(request.AnimalId, ct)
            ?? throw PublicException.NotFound("Animal not found.");
        await _authorization.EnsureFarmAccessAsync(animal.FarmId, ct);
        if (request.Active) animal.Activate(); else animal.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return (await AnimalMapping.ToDtosAsync([animal], _unitOfWork, ct))[0];
    }
}

/// <summary>Uploads a profile photo for an animal (JPEG, PNG, WebP or HEIC; max 8 MB).</summary>
public record SetAnimalPhotoCommand(Guid AnimalId, byte[] Image) : IRequest<AnimalDto>;

public class SetAnimalPhotoCommandHandler : IRequestHandler<SetAnimalPhotoCommand, AnimalDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IImageStorage _storage;

    public SetAnimalPhotoCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IImageStorage storage)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _storage = storage;
    }

    public async Task<AnimalDto> Handle(SetAnimalPhotoCommand request, CancellationToken ct)
    {
        var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(request.AnimalId, ct)
            ?? throw PublicException.NotFound("Animal not found.");
        await _authorization.EnsureFarmAccessAsync(animal.FarmId, ct);
        var contentType = UploadRules.ValidateImage(request.Image);
        var stored = await _storage.StoreAsync(request.Image, contentType, "farmhand/animals", ct);
        animal.SetPhoto(stored.Url);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await AnimalMapping.ToDtosAsync([animal], _unitOfWork, ct))[0];
    }
}

public record GetAnimalsQuery(Guid FarmId, Guid? CategoryId, string? Q, string? Species, bool IncludeInactive = false) : IRequest<IReadOnlyList<AnimalDto>>;

public class GetAnimalsQueryHandler : IRequestHandler<GetAnimalsQuery, IReadOnlyList<AnimalDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public GetAnimalsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<AnimalDto>> Handle(GetAnimalsQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var animals = await _unitOfWork.LivestockAnimals.SearchAsync(request.FarmId, request.CategoryId, request.Q, request.Species, request.IncludeInactive, ct);
        return await AnimalMapping.ToDtosAsync(animals, _unitOfWork, ct);
    }
}

public record GetAnimalQuery(Guid AnimalId) : IRequest<AnimalDetailDto>;

public class GetAnimalQueryHandler : IRequestHandler<GetAnimalQuery, AnimalDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ObservationProjector _projector;

    public GetAnimalQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ObservationProjector projector)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _projector = projector;
    }

    public async Task<AnimalDetailDto> Handle(GetAnimalQuery request, CancellationToken ct)
    {
        var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(request.AnimalId, ct)
            ?? throw PublicException.NotFound("Animal not found.");
        await _authorization.EnsureFarmAccessAsync(animal.FarmId, ct);

        var observations = (await _unitOfWork.PoultryObservations.GetByAnimalIdAsync(animal.Id, ct)).Take(30).ToList();
        var incidents = await _unitOfWork.Incidents.SearchAsync(
            new IncidentSearch([animal.FarmId], null, null, null, false, animal.Id, null, null, null, null, new Paging(1, 50)), ct);

        return new AnimalDetailDto(
            (await AnimalMapping.ToDtosAsync([animal], _unitOfWork, ct))[0],
            await _projector.SummariesAsync(observations, ct),
            incidents.Items.OrderByDescending(i => i.DetectedAt).Select(i => i.ToDto()).ToList());
    }
}

/// <summary>Groups (flocks, herds, pens) with head counts and registered-animal counts.</summary>
public record GetLivestockGroupsQuery(Guid FarmId) : IRequest<IReadOnlyList<LivestockCategoryDetailDto>>;

public class GetLivestockGroupsQueryHandler : IRequestHandler<GetLivestockGroupsQuery, IReadOnlyList<LivestockCategoryDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public GetLivestockGroupsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<LivestockCategoryDetailDto>> Handle(GetLivestockGroupsQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var categories = await _unitOfWork.LivestockCategories.GetByFarmIdAsync(request.FarmId, ct);
        var counts = await _unitOfWork.LivestockAnimals.CountActiveByCategoryAsync(request.FarmId, ct);
        return categories
            .OrderBy(c => c.Name)
            .Select(c => new LivestockCategoryDetailDto(c.Id, c.FarmId, c.Name, c.Description, c.Species, c.AnimalCount,
                counts.TryGetValue(c.Id, out var n) ? n : 0, c.CreatedAt))
            .ToList();
    }
}

public record UpdateLivestockGroupCommand(Guid CategoryId, string Name, string? Description, string? Species, int? Headcount) : IRequest<LivestockCategoryDetailDto>;

public class UpdateLivestockGroupCommandValidator : AbstractValidator<UpdateLivestockGroupCommand>
{
    public UpdateLivestockGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Species).MaximumLength(50);
        RuleFor(x => x.Headcount).InclusiveBetween(0, 1_000_000).When(x => x.Headcount is not null);
    }
}

public class UpdateLivestockGroupCommandHandler : IRequestHandler<UpdateLivestockGroupCommand, LivestockCategoryDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public UpdateLivestockGroupCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<LivestockCategoryDetailDto> Handle(UpdateLivestockGroupCommand request, CancellationToken ct)
    {
        var category = await _unitOfWork.LivestockCategories.GetByIdAsync(request.CategoryId, ct)
            ?? throw PublicException.NotFound("Group not found.");
        await _authorization.EnsureFarmAccessAsync(category.FarmId, ct);
        category.UpdateDetails(request.Name.Trim(), CreateAnimalCommandHandler.Trim(request.Description), CreateAnimalCommandHandler.Trim(request.Species), request.Headcount);
        await _unitOfWork.SaveChangesAsync(ct);
        var counts = await _unitOfWork.LivestockAnimals.CountActiveByCategoryAsync(category.FarmId, ct);
        return new LivestockCategoryDetailDto(category.Id, category.FarmId, category.Name, category.Description, category.Species,
            category.AnimalCount, counts.TryGetValue(category.Id, out var n) ? n : 0, category.CreatedAt);
    }
}
