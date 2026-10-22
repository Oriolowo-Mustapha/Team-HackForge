namespace FarmHand.Application.Common.DTOs;

/// <summary>Treatment plan summary.</summary>
/// <param name="Id">Treatment plan id.</param>
/// <param name="VetRequestId">Related vet request id.</param>
/// <param name="AuthoredByVetId">Vet who authored the plan.</param>
/// <param name="InstructionsText">Care instructions.</param>
/// <param name="Language">Language of the instructions.</param>
public record TreatmentPlanDto(
    Guid Id,
    Guid VetRequestId,
    string AuthoredByVetId,
    string InstructionsText,
    string? Language);
