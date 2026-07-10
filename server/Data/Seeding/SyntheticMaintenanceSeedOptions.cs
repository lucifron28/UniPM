namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticMaintenanceSeedOptions
{
    public const string SupportedDatasetVersion = "1.1.0";
    public const string DatasetFileName = "synthetic-maintenance-v1.json";

    public string DatasetPath { get; init; } = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "Seeding",
        "Resources",
        DatasetFileName);

    public static IReadOnlySet<string> AllowedActorRoles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "GSD_FIRE_INSPECTOR",
        "ELECTRICAL_ENGINEER",
        "PLUMBER",
        "PPF_SUPERVISOR",
        "DEPARTMENT_HEAD"
    };
}
