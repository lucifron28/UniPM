namespace UniPM.Api.Data.Seeding;

internal static class SyntheticActorRoleCatalog
{
    // These tokens identify synthetic fixture actors only. They are not authorization roles.
    internal static IReadOnlyList<string> SeedOnlyValues { get; } =
    [
        "GSD_FIRE_INSPECTOR",
        "ELECTRICAL_ENGINEER",
        "PLUMBER",
        "PPF_SUPERVISOR",
        "DEPARTMENT_HEAD"
    ];

    internal static IReadOnlySet<string> SeedOnlyCodes { get; } =
        SeedOnlyValues.ToHashSet(StringComparer.Ordinal);
}
