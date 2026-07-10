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

    public static IReadOnlySet<string> SupportedIssueKeys { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "alarm_panel_fault",
        "battery_issue",
        "clogged_filter",
        "device_not_responding",
        "expired_unit",
        "leaking",
        "low_pressure",
        "not_lighting",
        "smoke_detector_issue",
        "uv_light_issue",
        "weak_water_flow"
    };
}
