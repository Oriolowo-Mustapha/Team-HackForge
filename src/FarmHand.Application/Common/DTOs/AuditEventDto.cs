namespace FarmHand.Application.Common.DTOs;

/// <summary>Audit event summary.</summary>
/// <param name="Id">Audit event id.</param>
/// <param name="ActorId">Id of the actor who performed the event.</param>
/// <param name="EventType">Type of event.</param>
/// <param name="RelatedRecordId">Optional related record id.</param>
/// <param name="Timestamp">UTC event timestamp.</param>
/// <param name="Details">Optional details.</param>
public record AuditEventDto(
    Guid Id,
    string ActorId,
    string EventType,
    Guid? RelatedRecordId,
    DateTime Timestamp,
    string? Details);
