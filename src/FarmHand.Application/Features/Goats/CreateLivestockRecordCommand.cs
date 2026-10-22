using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Goats;

/// <summary>Creates a livestock record.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
/// <param name="CategoryId">Livestock category id (e.g. Poultry, Goats).</param>
/// <param name="Species">Species, selected from a fixed dropdown.</param>
/// <param name="AnimalName">Animal name or tag, selected from a fixed dropdown.</param>
/// <param name="Sex">Sex of the animal.</param>
/// <param name="ApproximateAge">Approximate age in months.</param>
public record CreateLivestockRecordCommand(
    Guid FarmId,
    Guid CategoryId,
    LivestockSpecies Species,
    AnimalName AnimalName,
    string Sex,
    int? ApproximateAge) : IRequest<Guid>;

public class CreateLivestockRecordCommandHandler : IRequestHandler<CreateLivestockRecordCommand, Guid>
{
    private readonly ILivestockAnimalRepository _livestockRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateLivestockRecordCommandHandler(
        ILivestockAnimalRepository livestockRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _livestockRepository = livestockRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateLivestockRecordCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        var category = await _unitOfWork.LivestockCategories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new KeyNotFoundException($"Category {request.CategoryId} not found.");
        if (category.FarmId != request.FarmId)
            throw new ArgumentException("The selected category does not belong to this farm.");

        var record = new LivestockAnimal(
            request.FarmId,
            request.CategoryId,
            request.AnimalName.ToString(),
            request.Species.ToString(),
            null,
            request.ApproximateAge,
            request.Sex);

        await _livestockRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return record.Id;
    }
}

public class CreateLivestockRecordCommandValidator : AbstractValidator<CreateLivestockRecordCommand>
{
    public CreateLivestockRecordCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x.AnimalName).IsInEnum();
        RuleFor(x => x.Species).IsInEnum();
        RuleFor(x => x.Sex).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ApproximateAge).GreaterThanOrEqualTo(0).When(x => x.ApproximateAge is not null);
    }
}
