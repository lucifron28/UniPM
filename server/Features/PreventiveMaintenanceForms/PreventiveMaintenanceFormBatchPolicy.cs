using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.PreventiveMaintenanceForms;

internal static class PreventiveMaintenanceFormBatchPolicy
{
    internal const string UniqueIndexName = "IX_PreventiveMaintenanceForms_Department_AssetCategory_PmCycle";

    internal static bool Matches(
        PreventiveMaintenanceForm form,
        PreventiveMaintenanceSchedule schedule)
    {
        if (schedule.Asset is not { } asset)
        {
            return false;
        }

        return HasResolvedDepartment(form, schedule)
            && NullableTextEquals(form.Department, asset.Department)
            && NullableTextEquals(form.AssetCategory, asset.AssetCategory)
            && form.PmCycle is not null
            && PreventiveMaintenanceCycle.Matches(
                form.PmCycle,
                PreventiveMaintenanceCycle.ForSchedule(schedule));
    }

    internal static bool HasResolvedDepartment(
        PreventiveMaintenanceForm form,
        PreventiveMaintenanceSchedule schedule)
    {
        return !string.IsNullOrWhiteSpace(form.Department)
            && !string.IsNullOrWhiteSpace(schedule.Asset?.Department);
    }

    internal static string? NormalizeDepartment(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    internal static bool IsSameDepartment(string? left, string? right)
    {
        return string.Equals(
            NormalizeDepartment(left),
            NormalizeDepartment(right),
            StringComparison.Ordinal);
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

    internal static bool IsCancelledScheduleStatus(string? status)
    {
        return string.Equals(status, ScheduleStatusCatalog.Cancelled, StringComparison.OrdinalIgnoreCase);
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
