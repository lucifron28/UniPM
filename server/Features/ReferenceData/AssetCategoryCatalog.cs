namespace UniPM.Api.Features.ReferenceData;

public sealed record AssetCategoryResponse(string Code, string DisplayName);

internal static class AssetCategoryCatalog
{
    internal static IReadOnlyList<AssetCategoryResponse> All { get; } =
    [
        new("fire-extinguisher", "Fire Extinguisher"),
        new("fire-alarm", "Fire Alarm"),
        new("emergency-light", "Emergency Light"),
        new("water-drinking-station", "Water Drinking Station")
    ];
}
