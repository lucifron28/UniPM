namespace UniPM.Api.Features.Schedules;

internal static class ScheduleSemesterCatalog
{
    internal const string First = "First";
    internal const string Second = "Second";
    internal const string Summer = "Summer";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [First, Second, Summer];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    // Semester is currently fixture/read-side data only; no API command writes it.
    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal);
}
