namespace FarmHand.Application.Common.DTOs;

/// <summary>Grouped action items shown on the farm dashboard.</summary>
/// <param name="Urgent">Actions for critical or high-severity incidents.</param>
/// <param name="Scheduled">Actions for incidents requiring follow-up.</param>
/// <param name="Routine">Static routine husbandry reminders.</param>
public record ActionPlanDto(
    List<string> Urgent,
    List<string> Scheduled,
    List<string> Routine);
