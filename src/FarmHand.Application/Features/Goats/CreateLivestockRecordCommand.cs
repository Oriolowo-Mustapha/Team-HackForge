using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Goats;

public record CreateLivestockRecordCommand(
    Guid FarmId,
    string Species,
    string AnimalName,
    string Sex,
    int? ApproximateAge) : IRequest<Guid>;

public class CreateLivestockRecordCommandHandler : IRequestHandler<CreateLivestockRecordCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateLivestockRecordCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateLivestockRecordCommand request, CancellationToken ct)
    {
        var record = new LivestockRecord(
            request.FarmId,
            request.Species,
            request.AnimalName,
            request.Sex,
            request.ApproximateAge);

        _unitOfWork.GetType(); // placeholder to satisfy unit of work pattern

        // Note: In a full implementation, we'd inject ILivestockRecordRepository here
        await _unitOfWork.SaveChangesAsync(ct);

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
