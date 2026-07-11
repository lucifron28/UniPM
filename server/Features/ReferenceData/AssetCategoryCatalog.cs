namespace UniPM.Api.Features.ReferenceData;

public sealed record AssetCategoryResponse(string Code, string DisplayName);

internal static class AssetCategoryCatalog
{
    internal const string FireExtinguisher = "fire-extinguisher";
    internal const string FireAlarm = "fire-alarm";
    internal const string EmergencyLight = "emergency-light";
    internal const string WaterDrinkingStation = "water-drinking-station";

    internal static IReadOnlyList<AssetCategoryResponse> All { get; } =
    [
        new(FireExtinguisher, "Fire Extinguisher"),
        new(FireAlarm, "Fire Alarm"),
        new(EmergencyLight, "Emergency Light"),
        new(WaterDrinkingStation, "Water Drinking Station")
    ];

    internal static IReadOnlyList<string> PersistedValues { get; } = All
        .Select(category => category.Code)
        .ToArray();

    internal static IReadOnlySet<string> PersistedCodes { get; } =
        PersistedValues.ToHashSet(StringComparer.Ordinal);

    internal static IReadOnlySet<string> ApiWritableCodes { get; } =
        PersistedCodes.ToHashSet(StringComparer.Ordinal);

    internal static bool ContainsCode(string? code)
    {
        return TryNormalize(code, out _);
    }

    internal static bool TryNormalize(string? code, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var candidate = code.Trim().ToLowerInvariant();
        if (!PersistedCodes.Contains(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
