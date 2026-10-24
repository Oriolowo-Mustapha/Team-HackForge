namespace FarmHand.Domain.Interfaces;

/// <summary>A page of results plus the total number of matching rows.</summary>
public record PagedList<T>(IReadOnlyList<T> Items, int TotalCount);

/// <summary>Normalised paging parameters (1-based page, page size clamped to 1..100).</summary>
public readonly record struct Paging
{
    public Paging(int page, int pageSize)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
}

public record ObservationSearch(
    IReadOnlyCollection<Guid> FarmIds,
    ObservationConfirmationStatus? Status,
    Guid? AnimalId,
    Guid? CategoryId,
    Paging Paging);

public record IncidentSearch(
    IReadOnlyCollection<Guid>? FarmIds,
    IReadOnlyCollection<Guid>? IncidentIds,
    IncidentStatus? Status,
    Severity? Severity,
    bool OpenOnly,
    Guid? AnimalId,
    Guid? CategoryId,
    DateTime? From,
    DateTime? To,
    string? Text,
    Paging Paging,
    Guid? AssignedVetPartnerId = null,
    bool NewestFirst = false,
    DateTime? ResolvedSince = null);

/// <summary>
/// Vet request filter. <see cref="FarmIds"/> restricts to farms (farmer scope);
/// <see cref="AssignedVetPartnerId"/> restricts to one vet's assignments;
/// <see cref="OpenForVetPartnerId"/> returns pending requests that vet may accept.
/// </summary>
public record VetRequestSearch(
    IReadOnlyCollection<Guid>? FarmIds,
    Guid? AssignedVetPartnerId,
    Guid? OpenForVetPartnerId,
    RequestStatus? Status,
    Guid? IncidentId,
    Paging Paging);

public record PaymentSearch(
    IReadOnlyCollection<Guid>? FarmIds,
    Guid? VetPartnerId,
    Guid? VetRequestId,
    string? Status,
    Paging Paging);

public record UserSearch(string? Role, string? Text, bool? IsActive, Paging Paging);

public record AuditEventSearch(string? ActorId, string? EventType, Guid? RelatedRecordId, Paging Paging);

/// <summary>Row returned by vet-request searches, joined with the incident and farm it belongs to.</summary>
public record VetRequestRow(VetRequest Request, Incident Incident, Farm Farm);

/// <summary>Row returned by payment searches, joined with the request and farm it belongs to.</summary>
public record PaymentRow(Payment Payment, VetRequest Request, Guid IncidentId, Farm Farm);

public record CountByKey(string Key, int Count);

public record PlatformOverview(
    IReadOnlyList<CountByKey> UsersByRole,
    int ActiveUsers,
    int Farms,
    int Animals,
    int FlockHeadcount,
    IReadOnlyList<CountByKey> ObservationsByStatus,
    IReadOnlyList<CountByKey> IncidentsByStatus,
    IReadOnlyList<CountByKey> IncidentsBySeverity,
    IReadOnlyList<CountByKey> VetRequestsByStatus,
    IReadOnlyList<CountByKey> PaymentsByStatus,
    decimal CompletedLivePaymentsTotal,
    decimal CompletedSimulatedPaymentsTotal,
    decimal PlatformCommissionTotal);
