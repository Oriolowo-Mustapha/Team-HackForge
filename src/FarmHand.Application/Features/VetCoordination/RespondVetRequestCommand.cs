using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Marks a vet request as responded. Veterinarian or admin only.</summary>
/// <param name="VetRequestId">Vet request id; must match the route value.</param>
/// <param name="VetPartnerId">Optional responding vet partner id; vets can only use their own profile.</param>
public record RespondVetRequestCommand(Guid VetRequestId, Guid? VetPartnerId = null) : IRequest;

public class RespondVetRequestCommandHandler : IRequestHandler<RespondVetRequestCommand>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IVetPartnerRepository _vetPartnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ICurrentUserService _currentUser;

    public RespondVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IVetPartnerRepository vetPartnerRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        ICurrentUserService currentUser)
    {
        _vetRequestRepository = vetRequestRepository;
        _vetPartnerRepository = vetPartnerRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RespondVetRequestCommand request, CancellationToken ct)
    {
        _authorization.EnsureVetOrAdmin();

        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException($"Vet request {request.VetRequestId} not found.");

        var vetPartnerId = request.VetPartnerId;

        if (!_currentUser.IsAdmin)
        {
            var partner = _currentUser.UserId is { } userId
                ? await _vetPartnerRepository.GetByUserIdAsync(userId, ct)
                : null;

            if (partner is null || !partner.IsActive)
                throw new UnauthorizedAccessException("No active vet profile is linked to this account.");

            if (request.VetPartnerId is { } requestedId && requestedId != partner.Id)
                throw new UnauthorizedAccessException("You can only respond as your own vet profile.");

            vetPartnerId = partner.Id;
        }

        vetRequest.Respond(vetPartnerId);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class RespondVetRequestCommandValidator : AbstractValidator<RespondVetRequestCommand>
{
    public RespondVetRequestCommandValidator()
    {
        RuleFor(x => x.VetRequestId).NotEqual(Guid.Empty);
    }
}
