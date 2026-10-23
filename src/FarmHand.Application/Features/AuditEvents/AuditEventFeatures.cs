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
    private readonly ICurrentUserService _currentUser;

    public CreateAuditEventCommandHandler(
        IAuditEventRepository auditRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateAuditEventCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can create audit events.");

        var actorId = _currentUser.UserId is { } userId
            ? userId.ToString()
            : request.ActorId;

        var auditEvent = new AuditEvent(actorId, request.EventType, request.RelatedRecordId, request.Details);
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

/// <summary>List audit events (administrators only), optionally filtered by actor, type or related record.</summary>
/// <param name="ActorId">Optional actor id filter.</param>
/// <param name="EventType">Optional event type filter.</param>
/// <param name="RelatedRecordId">Optional related record filter.</param>
public record GetAuditEventsQuery(string? ActorId, string? EventType, Guid? RelatedRecordId = null, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<AuditEventDto>>;

public class GetAuditEventsQueryHandler : IRequestHandler<GetAuditEventsQuery, PagedResult<AuditEventDto>>
{
    private readonly IAuditEventRepository _auditRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAuditEventsQueryHandler(IAuditEventRepository auditRepository, ICurrentUserService currentUser)
    {
        _auditRepository = auditRepository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AuditEventDto>> Handle(GetAuditEventsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            throw new UnauthorizedAccessException("Only administrators can read audit events.");

        var paging = new Paging(request.Page, request.PageSize);
        var page = await _auditRepository.SearchAsync(new AuditEventSearch(request.ActorId, request.EventType, request.RelatedRecordId, paging), ct);
        var items = page.Items.Select(e => new AuditEventDto(e.Id, e.ActorId, e.EventType, e.RelatedRecordId, e.Timestamp, e.Details)).ToList();
        return new PagedResult<AuditEventDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}

public class GetAuditEventsQueryValidator : AbstractValidator<GetAuditEventsQuery>
{
    public GetAuditEventsQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
