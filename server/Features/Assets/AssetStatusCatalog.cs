namespace UniPM.Api.Features.Assets;

internal static class AssetStatusCatalog
{
    internal const string Active = "Active";
    internal const string Inactive = "Inactive";
    internal const string Retired = "Retired";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [Active, Inactive, Retired];

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    // Asset creation currently writes only the default Active status.
    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        new HashSet<string>([Active], StringComparer.Ordinal);

    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var canonical = PersistedValues.FirstOrDefault(
            persisted => string.Equals(persisted, candidate, StringComparison.OrdinalIgnoreCase));

        if (canonical is null)
        {
            return false;
        }

        normalized = canonical;
        return true;
    }
}
