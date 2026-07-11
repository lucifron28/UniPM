namespace UniPM.Api.Features.Schedules;

internal static class SchedulePeriodTypeCatalog
{
    internal const string Quarter = "Quarter";
    internal const string Semester = "Semester";
    internal const string Annual = "Annual";
    internal const string Custom = "Custom";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [Quarter, Semester, Annual, Custom];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        PersistedCodes.ToHashSet(StringComparer.Ordinal);

    internal static bool TryNormalize(string? value, out string normalized)
    {
        return TryNormalize(value, PersistedValues, out normalized);
    }

    private static bool TryNormalize(
        string? value,
        IReadOnlyList<string> allowedValues,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var canonical = allowedValues.FirstOrDefault(
            allowed => string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase));

        if (canonical is null)
        {
            return false;
        }

        normalized = canonical;
        return true;
    }
}
