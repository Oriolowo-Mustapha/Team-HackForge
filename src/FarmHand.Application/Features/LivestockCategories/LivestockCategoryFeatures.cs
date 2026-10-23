using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.LivestockCategories;

/// <summary>Create a livestock category for a farm.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
/// <param name="Name">Category name, for example "Goats" or "Poultry".</param>
/// <param name="Description">Optional description.</param>
/// <param name="Species">Optional species for flock-level groups, e.g. "Chicken".</param>
/// <param name="Headcount">Optional head count maintained by the farmer for flocks.</param>
public record CreateLivestockCategoryCommand(Guid FarmId, string Name, string? Description, string? Species = null, int? Headcount = null) : IRequest<Guid>;

public class CreateLivestockCategoryCommandHandler : IRequestHandler<CreateLivestockCategoryCommand, Guid>
{
    private readonly ILivestockCategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateLivestockCategoryCommandHandler(
        ILivestockCategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateLivestockCategoryCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var category = new LivestockCategory(request.FarmId, request.Name.Trim(), request.Description?.Trim(), request.Species?.Trim(), request.Headcount ?? 0);
        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.Id;
    }
}

public class CreateLivestockCategoryCommandValidator : AbstractValidator<CreateLivestockCategoryCommand>
{
    public CreateLivestockCategoryCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Species).MaximumLength(50);
        RuleFor(x => x.Headcount).InclusiveBetween(0, 1_000_000).When(x => x.Headcount is not null);
    }
}

/// <summary>List livestock categories for a farm.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
public record GetLivestockCategoriesQuery(Guid FarmId) : IRequest<List<LivestockCategoryDto>>;

public class GetLivestockCategoriesQueryHandler : IRequestHandler<GetLivestockCategoriesQuery, List<LivestockCategoryDto>>
{
    private readonly ILivestockCategoryRepository _categoryRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetLivestockCategoriesQueryHandler(
        ILivestockCategoryRepository categoryRepository,
        IResourceAuthorizationService authorization)
    {
        _categoryRepository = categoryRepository;
        _authorization = authorization;
    }

    public async Task<List<LivestockCategoryDto>> Handle(GetLivestockCategoriesQuery request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var categories = await _categoryRepository.GetByFarmIdAsync(request.FarmId, ct);

        return categories.Select(c => new LivestockCategoryDto(
            c.Id, c.FarmId, c.Name, c.Description, c.AnimalCount)).ToList();
    }
}

public class GetLivestockCategoriesQueryValidator : AbstractValidator<GetLivestockCategoriesQuery>
{
    public GetLivestockCategoriesQueryValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
    }
}
