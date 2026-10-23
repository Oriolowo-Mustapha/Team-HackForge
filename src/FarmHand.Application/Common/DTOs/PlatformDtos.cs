using FarmHand.Domain.Enums;

namespace FarmHand.Application.Common.DTOs;

/// <summary>Farm with headline counts, returned by <c>GET /api/farms</c>.</summary>
public record FarmSummaryDto(
    Guid Id,
    string DisplayName,
    string Location,
    DateTime CreatedAt,
    int CategoryCount,
    int AnimalCount,
    int FlockHeadcount,
    int OpenIncidentCount,
    int PendingObservationCount,
    double? Latitude = null,
    double? Longitude = null,
    string? CityOrTown = null,
    string? State = null,
    string? Country = null,
    string? LocationSource = null);

/// <summary>Livestock group (flock, herd or pen). <see cref="Headcount"/> is maintained by the farmer for flocks.</summary>
public record LivestockCategoryDetailDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string? Description,
    string? Species,
    int Headcount,
    int RegisteredAnimals,
    DateTime CreatedAt);

/// <summary>Individual animal with its latest health state.</summary>
public record AnimalDto(
    Guid Id,
    Guid FarmId,
    Guid CategoryId,
    string? CategoryName,
    string Name,
    string Species,
    string? Breed,
    int? AgeMonths,
    string? Sex,
    string? Notes,
    string? PhotoUrl,
    bool IsActive,
    DateTime CreatedAt,
    string? DiseaseHistory,
    DateTime? RecentGestationDate,
    DateTime? LastVeterinaryVisit,
    string? Vaccinations,
    DateTime? LastLactationDate,
    string? Parentage,
    int OpenIncidentCount,
    Severity? HighestOpenSeverity,
    DateTime? LastObservedAt);

public record AnimalDetailDto(
    AnimalDto Animal,
    IReadOnlyList<ObservationSummaryDto> Observations,
    IReadOnlyList<IncidentDto> Incidents);

/// <summary>Observation summary for lists and timelines.</summary>
public record ObservationSummaryDto(
    Guid Id,
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string? SubjectName,
    DateTime RecordedAt,
    int MortalityCount,
    string? FarmerNotes,
    string? ImageUrl,
    ObservationConfirmationStatus ConfirmationStatus,
    string? AnalysisSource,
    string? InputChannels,
    Severity? AssessedSeverity,
    Guid? IncidentId);

/// <summary>AI or rule-based assessment attached to a confirmed observation.</summary>
public record AssessmentDto(
    Severity Severity,
    string Summary,
    string RecommendedAction,
    bool RequiresVetConsultation,
    string? Source,
    bool IsAi,
    DateTime AssessedAt);

/// <summary>Image description from the vision model. Confidence is self-reported by the model, not calibrated.</summary>
public record VisionDto(
    string Description,
    IReadOnlyList<string> DetectedIssues,
    decimal? Confidence,
    string? Source,
    bool Available);

/// <summary>Full observation for the review screen.</summary>
public record ObservationDetailDto(
    Guid Id,
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string? SubjectName,
    string? SubjectSpecies,
    DateTime RecordedAt,
    int MortalityCount,
    string? WaterConsumption,
    string? FeedConsumption,
    string? EggProduction,
    string? FarmerNotes,
    string? RawTranscript,
    string? ImageUrl,
    VisionDto? Vision,
    ObservationConfirmationStatus ConfirmationStatus,
    string? AnalysisSource,
    bool AnalysisIsAi,
    IReadOnlyList<string> Symptoms,
    IReadOnlyList<string> MissingInformation,
    string? InputChannels,
    AssessmentDto? Assessment,
    Guid? IncidentId,
    bool Reused);

public record ParticipantDto(Guid Id, string Name, string? Email, string? Phone);

public record VetSummaryDto(Guid Id, string Name, string Specialization, string Contact, bool HasPayoutAccount);

public record PaymentDto(
    Guid Id,
    Guid VetRequestId,
    string Status,
    string Mode,
    decimal Amount,
    decimal PlatformCommission,
    decimal TotalAmount,
    decimal? VetPayoutAmount,
    string Currency,
    string TransactionReference,
    string? CheckoutUrl,
    bool VetSubaccountUsed,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? FarmName,
    Guid? IncidentId);

/// <summary>
/// Vet request with context. <see cref="Farmer"/> contact details are only included for the farm owner,
/// administrators and the vet assigned to the request.
/// </summary>
public record VetRequestDetailDto(
    Guid Id,
    Guid IncidentId,
    RequestType RequestType,
    RequestStatus Status,
    decimal FarmerCost,
    string Currency,
    string? Description,
    DateTime? PreferredVisitDate,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    DateTime? CompletedAt,
    string? CompletionNotes,
    DateTime? CancelledAt,
    DateTime? DeclinedAt,
    string? DeclineReason,
    VetSummaryDto? Vet,
    bool IsTargeted,
    string IncidentType,
    Severity IncidentSeverity,
    IncidentStatus IncidentStatus,
    string? SubjectName,
    string? SubjectSpecies,
    string FarmName,
    string FarmLocation,
    ParticipantDto? Farmer,
    string PaymentStatus,
    PaymentDto? LatestPayment,
    IReadOnlyList<string> AvailableActions);

public record TreatmentScheduleItemDto(Guid Id, string Action, DateTime DueDate, bool IsCompleted, DateTime? CompletedAt);

public record TreatmentPlanDetailDto(
    Guid Id,
    Guid VetRequestId,
    string AuthoredByVetId,
    string? AuthorName,
    string InstructionsText,
    string? Language,
    DateTime CreatedAt,
    IReadOnlyList<TreatmentScheduleItemDto> Schedule);

public record TimelineEntryDto(DateTime At, string Kind, string Title, string? Detail, string? Actor);

/// <summary>Everything about one health case for the case-detail screen.</summary>
public record IncidentCaseDto(
    IncidentDto Incident,
    string FarmName,
    string FarmLocation,
    string? SubjectName,
    string? SubjectSpecies,
    bool IsFlockLevel,
    ParticipantDto? Farmer,
    ObservationDetailDto? SourceObservation,
    IReadOnlyList<VetRequestDetailDto> VetRequests,
    IReadOnlyList<TreatmentPlanDetailDto> TreatmentPlans,
    PreventionPlanDto? PreventionPlan,
    IReadOnlyList<FollowUpDto> FollowUps,
    IReadOnlyList<IncidentActionDto> Actions,
    IReadOnlyList<TimelineEntryDto> Timeline,
    IReadOnlyList<IncidentStatus> AllowedTransitions,
    string Access,
    bool CanRequestVet);

public record DashboardCountsDto(
    int Animals,
    int FlockHeadcount,
    int Groups,
    int OpenIncidents,
    int UrgentIncidents,
    int PendingObservations,
    int ActiveVetRequests,
    int ObservationsLast7Days,
    int ResolvedLast30Days);

public record FollowUpReminderDto(Guid IncidentId, string Title, DateTime DueAt, string Kind, bool IsOverdue);

public record ActivityDto(DateTime At, string Kind, string Title, string? Detail, Guid? RelatedId, Severity? Severity);

public record SeverityCountDto(Severity Severity, int Count);

public record DailyCountDto(DateOnly Date, int Observations, int Deaths);

/// <summary>Farmer home dashboard. All figures are computed from the farm's own records.</summary>
public record FarmDashboardDto(
    FarmDto Farm,
    DashboardCountsDto Counts,
    IReadOnlyList<IncidentDto> AttentionIncidents,
    IReadOnlyList<ObservationSummaryDto> PendingObservations,
    IReadOnlyList<VetRequestDetailDto> ActiveVetRequests,
    IReadOnlyList<FollowUpReminderDto> Reminders,
    IReadOnlyList<ActivityDto> RecentActivity,
    IReadOnlyList<SeverityCountDto> OpenBySeverity,
    IReadOnlyList<DailyCountDto> Last14Days,
    string NextAction,
    string NextActionKind);

/// <summary>Incident row for case lists.</summary>
public record IncidentListItemDto(
    IncidentDto Incident,
    string FarmName,
    string? SubjectName,
    string? SubjectSpecies,
    bool IsFlockLevel,
    RequestStatus? VetRequestStatus,
    string? VetName);
