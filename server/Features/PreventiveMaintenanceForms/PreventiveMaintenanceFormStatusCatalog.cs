namespace UniPM.Api.Features.PreventiveMaintenanceForms;

internal static class PreventiveMaintenanceFormStatusCatalog
{
    internal const string Draft = "Draft";
    internal const string Submitted = "Submitted";
    internal const string Acknowledged = "Acknowledged";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [Draft, Submitted, Acknowledged];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

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
