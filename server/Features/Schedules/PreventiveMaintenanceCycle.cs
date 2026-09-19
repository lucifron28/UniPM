using System.Globalization;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal static class PreventiveMaintenanceCycle
{
    internal const int Length = 7;

    internal static string FromScheduleDate(DateTimeOffset scheduleDate)
    {
        return scheduleDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    internal static string ForSchedule(PreventiveMaintenanceSchedule schedule)
    {
        return string.IsNullOrWhiteSpace(schedule.PmCycle)
            ? FromScheduleDate(schedule.ScheduleDate)
            : schedule.PmCycle.Trim();
    }

    internal static bool Matches(string? left, string? right)
    {
        return string.Equals(
            Normalize(left),
            Normalize(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
