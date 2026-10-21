using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Goats;

public record CreateLivestockRecordCommand(
    Guid FarmId,
    Guid CategoryId,
    string Species,
    string AnimalName,
    string Sex,
    int? ApproximateAge) : IRequest<Guid>;

public class CreateLivestockRecordCommandHandler : IRequestHandler<CreateLivestockRecordCommand, Guid>
{
    private readonly ILivestockAnimalRepository _livestockRepository;

    public CreateLivestockRecordCommandHandler(ILivestockAnimalRepository livestockRepository)
    {
        _livestockRepository = livestockRepository;
    }

    public async Task<Guid> Handle(CreateLivestockRecordCommand request, CancellationToken ct)
    {
        var record = new LivestockAnimal(
            request.FarmId,
            request.CategoryId,
            request.AnimalName,
            request.Species,
            null, // breed
            request.ApproximateAge,
            request.Sex);

        await _livestockRepository.AddAsync(record, ct);
        return record.Id;
    }
}

public class CreateLivestockRecordCommandValidator : AbstractValidator<CreateLivestockRecordCommand>
{
    public CreateLivestockRecordCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.CategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x.Species).NotEmpty();
        RuleFor(x => x.AnimalName).NotEmpty();
    }
}
