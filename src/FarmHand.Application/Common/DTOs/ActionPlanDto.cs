namespace FarmHand.Application.Common.DTOs;

public record ActionPlanDto(
    List<string> Urgent,
    List<string> Scheduled,
    List<string> Routine);
