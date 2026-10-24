using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Farms;

/// <summary>Creates a farm owned by the authenticated caller with full location details.</summary>
/// <param name="DisplayName">Human-readable farm name.</param>
/// <param name="Location">Full formatted address or location summary.</param>
/// <param name="Latitude">Optional GPS latitude coordinate.</param>
/// <param name="Longitude">Optional GPS longitude coordinate.</param>
/// <param name="CityOrTown">Optional town or city name.</param>
/// <param name="State">Optional state/province name.</param>
/// <param name="Country">Optional country name (defaults to Nigeria).</param>
/// <param name="LocationSource">Origin of location data ('device_gps' or 'manual').</param>
public record CreateFarmCommand(
    string DisplayName,
    string Location,
    double? Latitude = null,
    double? Longitude = null,
    string? CityOrTown = null,
    string? State = null,
    string? Country = null,
    string? LocationSource = null) : IRequest<Guid>;

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

        var farm = new Farm(
            ownerId,
            request.DisplayName.Trim(),
            request.Location.Trim(),
            request.Latitude,
            request.Longitude,
            request.CityOrTown?.Trim(),
            request.State?.Trim(),
            request.Country?.Trim(),
            request.LocationSource?.Trim());

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
        RuleFor(x => x.Location).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue).WithMessage("Provide both latitude and longitude, or neither.");
        RuleFor(x => x.CityOrTown).MaximumLength(100);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.LocationSource).MaximumLength(50);
    }
}
