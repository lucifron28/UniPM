using System.Globalization;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal static class PreventiveMaintenanceCycle
{
    internal const int Length = 7;
    internal static readonly TimeSpan InstitutionalOffset = TimeSpan.FromHours(8);

    internal static string FromScheduleDate(DateTimeOffset scheduleDate)
    {
        return ToInstitutionalTime(scheduleDate).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    internal static DateTimeOffset ToInstitutionalTime(DateTimeOffset value)
    {
        return value.ToOffset(InstitutionalOffset);
    }

    internal static bool TryParse(
        string? value,
        out int year,
        out int month)
    {
        year = 0;
        month = 0;

        if (!DateTime.TryParseExact(
                value?.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        year = parsed.Year;
        month = parsed.Month;
        return true;
    }

    internal static DateTimeOffset DeadlineForCycle(string pmCycle)
    {
        if (!TryParse(pmCycle, out var year, out var month))
        {
            throw new ArgumentException("PM cycle must use the yyyy-MM format.", nameof(pmCycle));
        }

        var nextMonth = new DateTime(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Unspecified).AddMonths(1);
        var lastInstant = nextMonth.AddTicks(-1);
        return new DateTimeOffset(lastInstant, InstitutionalOffset);
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
