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
public record CreateLivestockCategoryCommand(Guid FarmId, string Name, string? Description) : IRequest<Guid>;

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

        var category = new LivestockCategory(request.FarmId, request.Name, request.Description);
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
