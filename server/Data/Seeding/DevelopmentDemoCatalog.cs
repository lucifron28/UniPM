namespace UniPM.Api.Data.Seeding;

public static class DevelopmentDemoCatalog
{
    public const string InspectorEmail = "inspector@unipm.local";
    public const string GsdEmail = "gsd@unipm.local";

    public static IReadOnlyList<DevelopmentDemoAsset> Assets { get; } =
    [
        new(
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            "scenario-a",
            "FE-CCMS-001",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "CL-01",
            "UNIPM-FE-CCMS-001"),
        new(
            Guid.Parse("10000000-0000-4000-8000-000000000002"),
            "scenario-a",
            "FE-CCMS-002",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "CL-03",
            "UNIPM-FE-CCMS-002"),
        new(
            Guid.Parse("10000000-0000-4000-8000-000000000003"),
            "scenario-a",
            "FE-CCMS-003",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "CL-04",
            "UNIPM-FE-CCMS-003"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000001"),
            "scenario-b",
            "FE-LIB-001",
            "fire-extinguisher",
            "University Library Complex",
            "Library",
            "Library reading area",
            "UNIPM-FE-LIB-001"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000002"),
            "scenario-b",
            "FE-LIB-002",
            "fire-extinguisher",
            "University Library Complex",
            "Library",
            "Library circulation area",
            "UNIPM-FE-LIB-002"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000003"),
            "scenario-b",
            "FE-LIB-003",
            "fire-extinguisher",
            "University Library Complex",
            "Library",
            "Library corridor",
            "UNIPM-FE-LIB-003"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000001"),
            "scenario-c",
            "EL-OSAS-001",
            "emergency-light",
            "Administration Building",
            "Office of Student Affairs and Services",
            "Common corridor",
            "UNIPM-EL-OSAS-001"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000002"),
            "scenario-c",
            "EL-OSAS-002",
            "emergency-light",
            "Administration Building",
            "Office of Student Affairs and Services",
            "Stairwell approach",
            "UNIPM-EL-OSAS-002"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000003"),
            "scenario-c",
            "EL-GSD-001",
            "emergency-light",
            "Administration Building",
            "General Services Department",
            "Common area",
            "UNIPM-EL-GSD-001")
    ];

    internal static IReadOnlyList<Guid> ScheduleIds { get; } =
    [
        Guid.Parse("11000000-0000-4000-8000-000000000001"),
        Guid.Parse("11000000-0000-4000-8000-000000000002"),
        Guid.Parse("11000000-0000-4000-8000-000000000003"),
        Guid.Parse("21000000-0000-4000-8000-000000000001"),
        Guid.Parse("21000000-0000-4000-8000-000000000002"),
        Guid.Parse("21000000-0000-4000-8000-000000000003"),
        Guid.Parse("31000000-0000-4000-8000-000000000001"),
        Guid.Parse("31000000-0000-4000-8000-000000000002"),
        Guid.Parse("31000000-0000-4000-8000-000000000003")
    ];

    internal static IReadOnlyList<Guid> FormIds { get; } =
    [
        Guid.Parse("22000000-0000-4000-8000-000000000001"),
        Guid.Parse("32000000-0000-4000-8000-000000000001")
    ];

    internal static IReadOnlyList<Guid> InspectionIds { get; } =
    [
        Guid.Parse("24000000-0000-4000-8000-000000000001"),
        Guid.Parse("24000000-0000-4000-8000-000000000002"),
        Guid.Parse("24000000-0000-4000-8000-000000000003"),
        Guid.Parse("34000000-0000-4000-8000-000000000001"),
        Guid.Parse("34000000-0000-4000-8000-000000000002")
    ];

    internal static Guid AcknowledgementId { get; } =
        Guid.Parse("35000000-0000-4000-8000-000000000001");
}

public sealed record DevelopmentDemoAsset(
    Guid Id,
    string Scenario,
    string AssetCode,
    string AssetCategory,
    string Building,
    string Department,
    string Location,
    string QrCodeValue);
