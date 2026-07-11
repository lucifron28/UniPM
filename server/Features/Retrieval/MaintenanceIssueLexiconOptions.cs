using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

public sealed class MaintenanceIssueLexiconOptions
{
    public const string SupportedLexiconVersion = "1.0.0";
    public const string LexiconFileName = "maintenance-issue-lexicon-v1.json";
    public const string SchemaFileName = "maintenance-issue-lexicon-v1.schema.json";

    public string LexiconPath { get; init; } = Path.Combine(
        AppContext.BaseDirectory,
        "Features",
        "Retrieval",
        "Resources",
        LexiconFileName);

    public static IReadOnlyDictionary<string, string> ApprovedIssueCategories { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["alarm_panel_fault"] = AssetCategoryCatalog.FireAlarm,
        ["battery_issue"] = AssetCategoryCatalog.EmergencyLight,
        ["clogged_filter"] = AssetCategoryCatalog.WaterDrinkingStation,
        ["device_not_responding"] = AssetCategoryCatalog.FireAlarm,
        ["expired_unit"] = AssetCategoryCatalog.FireExtinguisher,
        ["leaking"] = AssetCategoryCatalog.WaterDrinkingStation,
        ["low_pressure"] = AssetCategoryCatalog.FireExtinguisher,
        ["not_lighting"] = AssetCategoryCatalog.EmergencyLight,
        ["smoke_detector_issue"] = AssetCategoryCatalog.FireAlarm,
        ["uv_light_issue"] = AssetCategoryCatalog.WaterDrinkingStation,
        ["weak_water_flow"] = AssetCategoryCatalog.WaterDrinkingStation
    };

    public static IReadOnlySet<string> SupportedIssueKeys { get; } =
        ApprovedIssueCategories.Keys.ToHashSet(StringComparer.Ordinal);
}
