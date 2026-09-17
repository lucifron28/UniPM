using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.PreventiveMaintenanceForms;

internal static class PreventiveMaintenanceFormBatchPolicy
{
    internal static bool Matches(
        PreventiveMaintenanceForm form,
        PreventiveMaintenanceSchedule schedule)
    {
        if (schedule.Asset is not { } asset)
        {
            return false;
        }

        return NullableTextEquals(form.Department, asset.Department)
            && NullableTextEquals(form.AssetCategory, asset.AssetCategory)
            && NullableTextEquals(form.PeriodType, schedule.PeriodType)
            && NullableTextEquals(form.Quarter, schedule.Quarter)
            && NullableTextEquals(form.Semester, schedule.Semester)
            && form.Year == schedule.Year
            && NullableTextEquals(form.AcademicYear, schedule.AcademicYear);
    }

    internal static bool IsEligibleScheduleStatus(string? status)
    {
        return string.Equals(status, ScheduleStatusCatalog.Due, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ScheduleStatusCatalog.Ongoing, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ScheduleStatusCatalog.Overdue, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCompletedScheduleStatus(string? status)
    {
        return string.Equals(status, ScheduleStatusCatalog.Completed, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasCompletedExecution(InspectionRecord inspection)
    {
        return inspection.CompletedAt is not null;
    }

    private static bool NullableTextEquals(string? left, string? right)
    {
        return string.Equals(
            NormalizeNullableText(left),
            NormalizeNullableText(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
