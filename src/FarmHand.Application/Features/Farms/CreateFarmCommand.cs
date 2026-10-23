using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Farms;

/// <summary>Creates a farm owned by the authenticated caller.</summary>
/// <param name="DisplayName">Human-readable farm name.</param>
/// <param name="Location">Free-text location.</param>
public record CreateFarmCommand(string DisplayName, string Location) : IRequest<Guid>;

public class CreateFarmCommandHandler : IRequestHandler<CreateFarmCommand, Guid>
{
    private readonly IFarmRepository _farmRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateFarmCommandHandler(
        IFarmRepository farmRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _farmRepository = farmRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateFarmCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId?.ToString()
            ?? throw new UnauthorizedAccessException("An authenticated user is required to create a farm.");

        if (_currentUser.IsVet)
            throw new UnauthorizedAccessException("Veterinarian accounts cannot own farms.");

        var farm = new Farm(ownerId, request.DisplayName.Trim(), request.Location.Trim());
        await _farmRepository.AddAsync(farm, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return farm.Id;
    }
}

public class CreateFarmCommandValidator : AbstractValidator<CreateFarmCommand>
{
    public CreateFarmCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
    }
}
