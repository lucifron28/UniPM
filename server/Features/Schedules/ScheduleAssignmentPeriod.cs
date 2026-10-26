using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal sealed record ScheduleAssignmentPeriodResult(
    DateTimeOffset? InspectorAssignmentDueAt,
    string AssignmentPeriodStatus);

internal static class ScheduleAssignmentPeriod
{
    private const int InspectorAssignmentPeriodDays = 30;

    internal static ScheduleAssignmentPeriodResult Calculate(
        DateTimeOffset? firstInspectorAssignedAt,
        DateTimeOffset? completedAt,
        string scheduleStatus,
        DateTimeOffset now)
    {
        var dueAt = firstInspectorAssignedAt?.AddDays(InspectorAssignmentPeriodDays);

        if (string.Equals(scheduleStatus, ScheduleStatusCatalog.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return new ScheduleAssignmentPeriodResult(dueAt, "Cancelled");
        }

        if (dueAt is null)
        {
            return new ScheduleAssignmentPeriodResult(null, "Unavailable");
        }

        if (completedAt is { } completed)
        {
            return new ScheduleAssignmentPeriodResult(
                dueAt,
                completed <= dueAt.Value ? "CompletedWithinPeriod" : "CompletedLate");
        }

        if (string.Equals(scheduleStatus, ScheduleStatusCatalog.Completed, StringComparison.OrdinalIgnoreCase))
        {
            return new ScheduleAssignmentPeriodResult(dueAt, "Unavailable");
        }

        return new ScheduleAssignmentPeriodResult(
            dueAt,
            now <= dueAt.Value ? "WithinPeriod" : "Overdue");
    }
}
