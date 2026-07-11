namespace UniPM.Api.Features.Schedules;

internal static class ScheduleQuarterCatalog
{
    internal const string Q1 = "Q1";
    internal const string Q2 = "Q2";
    internal const string Q3 = "Q3";
    internal const string Q4 = "Q4";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [Q1, Q2, Q3, Q4];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        PersistedCodes.ToHashSet(StringComparer.Ordinal);

    internal static bool TryNormalizeNullable(string? value, out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
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
