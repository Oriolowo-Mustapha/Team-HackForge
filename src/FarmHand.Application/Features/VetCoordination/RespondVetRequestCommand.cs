using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Marks a vet request as responded.</summary>
/// <param name="VetRequestId">Vet request id; must match the route value.</param>
/// <param name="VetPartnerId">Optional responding vet partner id.</param>
public record RespondVetRequestCommand(Guid VetRequestId, Guid? VetPartnerId = null) : IRequest;

public class RespondVetRequestCommandHandler : IRequestHandler<RespondVetRequestCommand>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RespondVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _vetRequestRepository = vetRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(RespondVetRequestCommand request, CancellationToken ct)
    {
        var vetRequest = await _vetRequestRepository.GetByIdAsync(request.VetRequestId, ct)
            ?? throw new KeyNotFoundException($"Vet request {request.VetRequestId} not found.");

        vetRequest.Respond(request.VetPartnerId);
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
