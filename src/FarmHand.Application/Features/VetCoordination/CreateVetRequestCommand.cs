using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.VetCoordination;

/// <summary>Creates a vet request for an incident.</summary>
/// <param name="IncidentId">Incident id; must match the route value.</param>
/// <param name="RequestType">Coordination model, sent as an integer (0-2).</param>
/// <param name="FarmerCost">Farmer's share of the cost, in NGN.</param>
public record CreateVetRequestCommand(
    Guid IncidentId,
    RequestType RequestType,
    decimal FarmerCost) : IRequest<Guid>;

public class CreateVetRequestCommandHandler : IRequestHandler<CreateVetRequestCommand, Guid>
{
    private readonly IVetRequestRepository _vetRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVetRequestCommandHandler(
        IVetRequestRepository vetRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _vetRequestRepository = vetRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateVetRequestCommand request, CancellationToken ct)
    {
        var vetRequest = new VetRequest(
            request.IncidentId,
            request.RequestType,
            new Domain.ValueObjects.Money(request.FarmerCost));

        await _vetRequestRepository.AddAsync(vetRequest, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return vetRequest.Id;
    }
}

public class CreateVetRequestCommandValidator : AbstractValidator<CreateVetRequestCommand>
{
    public CreateVetRequestCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.FarmerCost).GreaterThanOrEqualTo(0);
    }
}
