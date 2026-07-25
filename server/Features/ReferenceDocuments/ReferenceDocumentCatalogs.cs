namespace UniPM.Api.Features.ReferenceDocuments;

internal static class EvidenceSourceGroupCatalog
{
    internal const string MaintenanceHistory = "MaintenanceHistory";
    internal const string InstitutionalReference = "InstitutionalReference";
    internal const string OemReference = "OemReference";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [
        MaintenanceHistory,
        InstitutionalReference,
        OemReference
    ];
}

internal static class ReferenceDocumentSourceTypeCatalog
{
    internal const string Institutional = "Institutional";
    internal const string Oem = "Oem";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [
        Institutional,
        Oem
    ];

    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var match = PersistedValues.FirstOrDefault(item =>
            string.Equals(item, candidate, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        normalized = match;
        return true;
    }
}

internal static class ReferenceDocumentLifecycleCatalog
{
    internal const string Active = "Active";
    internal const string Superseded = "Superseded";
    internal const string Archived = "Archived";

    internal static IReadOnlyList<string> PersistedValues { get; } =
    [
        Active,
        Superseded,
        Archived
    ];

    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        var match = PersistedValues.FirstOrDefault(item =>
            string.Equals(item, candidate, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        normalized = match;
        return true;
    }
}
