namespace FarmHand.Application.Common.DTOs;

/// <summary>Livestock category summary.</summary>
/// <param name="Id">Category id.</param>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="Name">Category name, for example "Goats" or "Poultry".</param>
/// <param name="Description">Optional description.</param>
/// <param name="AnimalCount">Number of animals in the category.</param>
public record LivestockCategoryDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string? Description,
    int AnimalCount);
