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
            "DEMO-FE-001",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "Ground Floor Computer Laboratory",
            "UNIPM-DEMO-FE-001"),
        new(
            Guid.Parse("10000000-0000-4000-8000-000000000002"),
            "scenario-a",
            "DEMO-FE-002",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "Second Floor Faculty Room",
            "UNIPM-DEMO-FE-002"),
        new(
            Guid.Parse("10000000-0000-4000-8000-000000000003"),
            "scenario-a",
            "DEMO-FE-003",
            "fire-extinguisher",
            "CCMS Building",
            "CCMS",
            "Third Floor Network Laboratory",
            "UNIPM-DEMO-FE-003"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000001"),
            "scenario-b",
            "DEMO-LIB-FE-001",
            "fire-extinguisher",
            "University Library",
            "Library",
            "Ground Floor Reading Area",
            "UNIPM-DEMO-LIB-FE-001"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000002"),
            "scenario-b",
            "DEMO-LIB-FE-002",
            "fire-extinguisher",
            "University Library",
            "Library",
            "Second Floor Periodicals Section",
            "UNIPM-DEMO-LIB-FE-002"),
        new(
            Guid.Parse("20000000-0000-4000-8000-000000000003"),
            "scenario-b",
            "DEMO-LIB-FE-003",
            "fire-extinguisher",
            "University Library",
            "Library",
            "Third Floor Archives Room",
            "UNIPM-DEMO-LIB-FE-003"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000001"),
            "scenario-c",
            "DEMO-EL-001",
            "emergency-light",
            "Administration Building",
            "Student Affairs Office",
            "Ground Floor Main Corridor",
            "UNIPM-DEMO-EL-001"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000002"),
            "scenario-c",
            "DEMO-EL-002",
            "emergency-light",
            "Administration Building",
            "Student Affairs Office",
            "Second Floor Stairwell",
            "UNIPM-DEMO-EL-002"),
        new(
            Guid.Parse("30000000-0000-4000-8000-000000000003"),
            "scenario-c",
            "DEMO-EL-003",
            "emergency-light",
            "Administration Building",
            "Student Affairs Office",
            "Third Floor Exit Hall",
            "UNIPM-DEMO-EL-003")
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
        Guid.Parse("34000000-0000-4000-8000-000000000002"),
        Guid.Parse("34000000-0000-4000-8000-000000000003")
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
