using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Goats;

/// <summary>Creates a livestock record. The handler adds it to the EF context but does not call SaveChanges.</summary>
/// <param name="FarmId">Owning farm id.</param>
/// <param name="Species">Species, for example "goat".</param>
/// <param name="AnimalName">Animal name or tag.</param>
/// <param name="Sex">Sex of the animal.</param>
/// <param name="ApproximateAge">Approximate age in months.</param>
public record CreateLivestockRecordCommand(
    Guid FarmId,
    string Species,
    string AnimalName,
    string Sex,
    int? ApproximateAge) : IRequest<Guid>;

public class CreateLivestockRecordCommandHandler : IRequestHandler<CreateLivestockRecordCommand, Guid>
{
    private readonly ILivestockRecordRepository _livestockRepository;

    public CreateLivestockRecordCommandHandler(ILivestockRecordRepository livestockRepository)
    {
        _livestockRepository = livestockRepository;
    }

    public async Task<Guid> Handle(CreateLivestockRecordCommand request, CancellationToken ct)
    {
        var record = new LivestockRecord(
            request.FarmId,
            request.Species,
            request.AnimalName,
            request.Sex,
            request.ApproximateAge);

        await _livestockRepository.AddAsync(record, ct);
        return record.Id;
    }
}

public class CreateLivestockRecordCommandValidator : AbstractValidator<CreateLivestockRecordCommand>
{
    public CreateLivestockRecordCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x.Species).NotEmpty();
        RuleFor(x => x.AnimalName).NotEmpty();
    }
}
