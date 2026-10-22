using FarmHand.Application.Common.DTOs;
using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.AuditEvents;

/// <summary>Record an audit event.</summary>
/// <param name="ActorId">Id of the actor who performed the event.</param>
/// <param name="EventType">Type of event.</param>
/// <param name="RelatedRecordId">Optional related record id.</param>
/// <param name="Details">Optional details.</param>
public record CreateAuditEventCommand(string ActorId, string EventType, Guid? RelatedRecordId, string? Details) : IRequest<Guid>;

public class CreateAuditEventCommandHandler : IRequestHandler<CreateAuditEventCommand, Guid>
{
    private readonly IAuditEventRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAuditEventCommandHandler(
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork)
    {
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateAuditEventCommand request, CancellationToken ct)
    {
        var auditEvent = new AuditEvent(request.ActorId, request.EventType, request.RelatedRecordId, request.Details);
        await _auditRepository.AddAsync(auditEvent, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return auditEvent.Id;
    }
}

public class CreateAuditEventCommandValidator : AbstractValidator<CreateAuditEventCommand>
{
    public CreateAuditEventCommandValidator()
    {
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Details).MaximumLength(1000);
    }
}

/// <summary>List audit events, optionally filtered by actor or event type.</summary>
/// <param name="ActorId">Optional actor id filter.</param>
/// <param name="EventType">Optional event type filter.</param>
public record GetAuditEventsQuery(string? ActorId, string? EventType) : IRequest<List<AuditEventDto>>;

public class GetAuditEventsQueryHandler : IRequestHandler<GetAuditEventsQuery, List<AuditEventDto>>
{
    private readonly IAuditEventRepository _auditRepository;

    public GetAuditEventsQueryHandler(IAuditEventRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<List<AuditEventDto>> Handle(GetAuditEventsQuery request, CancellationToken ct)
    {
        IEnumerable<AuditEvent> events;

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            events = await _auditRepository.GetByEventTypeAsync(request.EventType, default);
        }
        else if (!string.IsNullOrWhiteSpace(request.ActorId))
        {
            events = await _auditRepository.GetByActorIdAsync(request.ActorId, default);
        }
        else
        {
            events = await _auditRepository.GetAllAsync(default);
        }

        return events.Select(e => new AuditEventDto(
            e.Id, e.ActorId, e.EventType, e.RelatedRecordId, e.Timestamp, e.Details)).ToList();
    }
}

public class GetAuditEventsQueryValidator : AbstractValidator<GetAuditEventsQuery>
{
    public GetAuditEventsQueryValidator() { }
}
