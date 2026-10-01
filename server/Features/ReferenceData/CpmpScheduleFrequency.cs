namespace UniPM.Api.Features.ReferenceData;

internal static class CpmpScheduleFrequency
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<int>> MonthsByCategory =
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
        {
            [AssetCategoryCatalog.FireExtinguisher] = Array.AsReadOnly(new[] { 2, 5, 8, 11 }),
            [AssetCategoryCatalog.FireAlarm] = Array.AsReadOnly(new[] { 6, 12 }),
            [AssetCategoryCatalog.EmergencyLight] = Array.AsReadOnly(new[] { 6, 12 }),
            [AssetCategoryCatalog.WaterDrinkingStation] = Array.AsReadOnly(new[] { 2, 5, 8, 11 })
        };

    internal static IReadOnlyList<int> GetMonths(string? assetCategory)
    {
        return AssetCategoryCatalog.TryNormalize(assetCategory, out var normalized)
            && MonthsByCategory.TryGetValue(normalized, out var months)
                ? months
                : Array.Empty<int>();
    }

    internal static bool IsValid(string? assetCategory, int month)
    {
        return GetMonths(assetCategory).Contains(month);
    }
}
