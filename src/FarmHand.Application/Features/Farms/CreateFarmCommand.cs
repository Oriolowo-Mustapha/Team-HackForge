using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Farms;

public record CreateFarmCommand(string OwnerId, string DisplayName, string Location) : IRequest<Guid>;

public class CreateFarmCommandHandler : IRequestHandler<CreateFarmCommand, Guid>
{
    private readonly IFarmRepository _farmRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFarmCommandHandler(IFarmRepository farmRepository, IUnitOfWork unitOfWork)
    {
        _farmRepository = farmRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateFarmCommand request, CancellationToken ct)
    {
        var farm = new Farm(request.OwnerId, request.DisplayName, request.Location);
        await _farmRepository.AddAsync(farm, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return farm.Id;
    }
}

public class CreateFarmCommandValidator : AbstractValidator<CreateFarmCommand>
{
    public CreateFarmCommandValidator()
    {
        RuleFor(x => x.OwnerId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty();
        RuleFor(x => x.Location).NotEmpty();
    }
}
