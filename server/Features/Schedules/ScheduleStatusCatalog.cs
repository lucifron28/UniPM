namespace UniPM.Api.Features.Schedules;

internal static class ScheduleStatusCatalog
{
    internal const string Due = "Due";
    internal const string Ongoing = "Ongoing";
    internal const string Completed = "Completed";
    internal const string Overdue = "Overdue";
    internal const string Cancelled = "Cancelled";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [Due, Ongoing, Completed, Overdue, Cancelled];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    // Current commands write Due on schedule creation and Completed on inspection.
    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        new HashSet<string>([Due, Completed], StringComparer.Ordinal);

    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var canonical = PersistedValues.FirstOrDefault(
            allowed => string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase));

        if (canonical is null)
        {
            return false;
        }

        normalized = canonical;
        return true;
    }
}
