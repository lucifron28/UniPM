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
        ["alarm_panel_fault"] = "fire-alarm",
        ["battery_issue"] = "emergency-light",
        ["clogged_filter"] = "water-drinking-station",
        ["device_not_responding"] = "fire-alarm",
        ["expired_unit"] = "fire-extinguisher",
        ["leaking"] = "water-drinking-station",
        ["low_pressure"] = "fire-extinguisher",
        ["not_lighting"] = "emergency-light",
        ["smoke_detector_issue"] = "fire-alarm",
        ["uv_light_issue"] = "water-drinking-station",
        ["weak_water_flow"] = "water-drinking-station"
    };

    public static IReadOnlySet<string> SupportedIssueKeys { get; } =
        ApprovedIssueCategories.Keys.ToHashSet(StringComparer.Ordinal);
}
