using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.FollowUps;

/// <summary>Record a follow-up for an incident.</summary>
/// <param name="IncidentId">Incident id; must be accessible to the caller.</param>
/// <param name="FarmerResponse">Farmer-reported outcome.</param>
/// <param name="Notes">Optional follow-up notes.</param>
public record CreateFollowUpCommand(Guid IncidentId, Outcome FarmerResponse, string? Notes) : IRequest<Guid>;

public class CreateFollowUpCommandHandler : IRequestHandler<CreateFollowUpCommand, Guid>
{
    private readonly IFollowUpRepository _followUpRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;

    public CreateFollowUpCommandHandler(
        IFollowUpRepository followUpRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _followUpRepository = followUpRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(CreateFollowUpCommand request, CancellationToken ct)
    {
        var incident = await _unitOfWork.Incidents.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureIncidentWriteAccessAsync(incident.Id, ct);

        var followUp = new FollowUp(request.IncidentId, request.FarmerResponse, request.Notes);
        await _followUpRepository.AddAsync(followUp, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return followUp.Id;
    }
}

public class CreateFollowUpCommandValidator : AbstractValidator<CreateFollowUpCommand>
{
    public CreateFollowUpCommandValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
        RuleFor(x => x.FarmerResponse).IsInEnum();
    }
}

/// <summary>List follow-ups for an incident.</summary>
/// <param name="IncidentId">Incident id; must be accessible to the caller.</param>
public record GetFollowUpsQuery(Guid IncidentId) : IRequest<List<FollowUpDto>>;

public class GetFollowUpsQueryHandler : IRequestHandler<GetFollowUpsQuery, List<FollowUpDto>>
{
    private readonly IFollowUpRepository _followUpRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IResourceAuthorizationService _authorization;

    public GetFollowUpsQueryHandler(
        IFollowUpRepository followUpRepository,
        IIncidentRepository incidentRepository,
        IResourceAuthorizationService authorization)
    {
        _followUpRepository = followUpRepository;
        _incidentRepository = incidentRepository;
        _authorization = authorization;
    }

    public async Task<List<FollowUpDto>> Handle(GetFollowUpsQuery request, CancellationToken ct)
    {
        var incident = await _incidentRepository.GetByIdAsync(request.IncidentId, ct)
            ?? throw new KeyNotFoundException($"Incident {request.IncidentId} not found.");

        await _authorization.EnsureIncidentAccessAsync(incident.Id, ct);

        var followUps = await _followUpRepository.GetByIncidentIdAsync(request.IncidentId, ct);

        return followUps.Select(f => new FollowUpDto(
            f.Id, f.IncidentId, f.FarmerResponse, f.Notes, f.RecordedAt)).ToList();
    }
}

public class GetFollowUpsQueryValidator : AbstractValidator<GetFollowUpsQuery>
{
    public GetFollowUpsQueryValidator()
    {
        RuleFor(x => x.IncidentId).NotEqual(Guid.Empty);
    }
}
