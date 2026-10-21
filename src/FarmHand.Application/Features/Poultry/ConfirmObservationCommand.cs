using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Confirms an AI-extracted observation.</summary>
/// <param name="ObservationId">Observation id; must match the route value.</param>
/// <param name="CorrectedNotes">Optional corrected notes that replace the extracted farmer notes.</param>
public record ConfirmObservationCommand(Guid ObservationId, string? CorrectedNotes) : IRequest;

public class ConfirmObservationCommandHandler : IRequestHandler<ConfirmObservationCommand>
{
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmObservationCommandHandler(
        IPoultryObservationRepository observationRepository,
        IUnitOfWork unitOfWork)
    {
        _observationRepository = observationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ConfirmObservationCommand request, CancellationToken ct)
    {
        var observation = await _observationRepository.GetByIdAsync(request.ObservationId, ct)
            ?? throw new KeyNotFoundException($"Observation {request.ObservationId} not found.");

        observation.Confirm(request.CorrectedNotes);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class ConfirmObservationCommandValidator : AbstractValidator<ConfirmObservationCommand>
{
    public ConfirmObservationCommandValidator()
    {
        RuleFor(x => x.ObservationId).NotEqual(Guid.Empty);
    }
}
