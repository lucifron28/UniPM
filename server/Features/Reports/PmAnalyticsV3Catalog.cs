using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Reports;

public sealed record PmAnalyticsV3CategoryDefinition(
    string Id,
    string Label,
    IReadOnlyList<int> ScheduledMonths);

public sealed record PmAnalyticsV3MeasureDefinition(
    string Id,
    string Label,
    string ValueType,
    string Unit,
    string Grain,
    string Population,
    string Definition);

public sealed record PmAnalyticsV3DimensionDefinition(
    string Id,
    string Label,
    string ValueType,
    string NullSemantics,
    string Grain,
    string Definition);

public sealed record PmAnalyticsV3FilterFieldDefinition(
    string Id,
    string Label,
    bool IsBaseFilter,
    string ValueType,
    int MaximumLength,
    IReadOnlyList<string> AllowedValues,
    IReadOnlySet<PmAnalyticsV3FilterOperator> Operators);

public sealed record PmAnalyticsV3RecordColumnDefinition(
    string Id,
    string Label,
    string ValueType,
    string Unit,
    string? NullSemantics);

/// <summary>
/// Code-first identifiers for the initial PM execution subject. Definitions
/// describe the existing V2 data meaning; they do not bind model output to SQL.
/// </summary>
internal static class PmAnalyticsV3Catalog
{
    internal const int Version = 2;
    internal const string SubjectPmExecution = "pmExecution";
    internal const string SubjectAssetInventory = "assetInventory";

    internal const string ScheduledCount = "scheduledCount";
    internal const string CompletedCount = "completedCount";
    internal const string CompletedOnTimeCount = "completedOnTimeCount";
    internal const string CompletedLateCount = "completedLateCount";
    internal const string NonOperationalCount = "nonOperationalCount";
    internal const string RemainingCount = "remainingCount";
    internal const string MissedCount = "missedCount";
    internal const string ProgressPercent = "progressPercent";
    internal const string OnTimeCompliancePercent = "onTimeCompliancePercent";
    internal const string DistinctAssetCount = "distinctAssetCount";

    internal const string AssignmentCompletedWithinCount = "assignmentCompletedWithinCount";
    internal const string AssignmentCompletedLateCount = "assignmentCompletedLateCount";
    internal const string AssignmentOverdueCount = "assignmentOverdueCount";
    internal const string AssignmentOpenCount = "assignmentOpenCount";
    internal const string AssignmentUnknownCount = "assignmentUnknownCount";
    internal const string FirstSupervisorAssignedAt = "firstSupervisorAssignedAt";
    internal const string FirstInspectorAssignedAt = "firstInspectorAssignedAt";
    internal const string InspectorAssignmentDueAt = "inspectorAssignmentDueAt";
    internal const string AssignmentPeriodStatus = "assignmentPeriodStatus";

    internal const string AssetCategory = "assetCategory";
    internal const string Department = "department";
    internal const string Building = "building";
    internal const string PmCycle = "pmCycle";
    internal const string CalendarQuarter = "calendarQuarter";
    internal const string CalendarYear = "calendarYear";

    internal const string AssetId = "assetId";
    internal const string AssetCode = "assetCode";
    internal const string Location = "location";
    internal const string RegistrationStatus = "registrationStatus";
    internal const string RegisteredAssetCount = "registeredAssetCount";
    internal const string Deadline = "deadline";
    internal const string CompletedAt = "completedAt";
    internal const string ScheduleStatus = "scheduleStatus";
    internal const string FormStatus = "formStatus";
    internal const string Condition = "condition";
    internal const string Timeliness = "timeliness";
    internal const string UsedLegacyDateFallback = "usedLegacyDateFallback";

    internal static IReadOnlyList<PmAnalyticsV3CategoryDefinition> Categories { get; } =
        Array.AsReadOnly(AssetCategoryCatalog.All
            .Select(category => new PmAnalyticsV3CategoryDefinition(
                category.Code,
                category.DisplayName,
                Array.AsReadOnly(category.ScheduledMonths.ToArray())))
            .ToArray());

    internal static IReadOnlyList<PmAnalyticsV3SubjectDefinition> Subjects { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3SubjectDefinition(
                SubjectPmExecution,
                "PM execution",
                "Eligible persisted asset-cycle schedules and their inspection outcomes.",
                true),
            new PmAnalyticsV3SubjectDefinition(
                SubjectAssetInventory,
                "Asset inventory",
                "One current registered asset per row, independent of PM schedules and inspections.",
                false)
        });

    internal static IReadOnlyList<PmAnalyticsV3MeasureDefinition> Measures { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3MeasureDefinition(
                ScheduledCount,
                "Scheduled",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count eligible persisted asset-cycle rows; cancelled schedules are excluded."),
            new PmAnalyticsV3MeasureDefinition(
                CompletedCount,
                "Completed",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count eligible rows with an authoritative inspection completion timestamp."),
            new PmAnalyticsV3MeasureDefinition(
                CompletedOnTimeCount,
                "Completed on time",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count completed eligible rows whose completion is no later than the PM-cycle deadline."),
            new PmAnalyticsV3MeasureDefinition(
                CompletedLateCount,
                "Completed late",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count completed eligible rows whose completion is after the PM-cycle deadline."),
            new PmAnalyticsV3MeasureDefinition(
                NonOperationalCount,
                "Non-operational findings",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count completed eligible rows with a recorded non-operational inspection condition."),
            new PmAnalyticsV3MeasureDefinition(
                RemainingCount,
                "Remaining",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count incomplete eligible rows in a cycle whose deadline has not passed."),
            new PmAnalyticsV3MeasureDefinition(
                MissedCount,
                "Missed",
                "integer",
                "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "eligible population",
                "Count incomplete eligible rows whose PM-cycle deadline has passed."),
            new PmAnalyticsV3MeasureDefinition(
                ProgressPercent,
                "Progress",
                "decimal",
                "percent",
                "completed eligible asset-cycles divided by scheduled eligible asset-cycles",
                "eligible population",
                "Completed count divided by eligible scheduled count, multiplied by 100."),
            new PmAnalyticsV3MeasureDefinition(
                OnTimeCompliancePercent,
                "On-time compliance",
                "decimal",
                "percent",
                "on-time eligible asset-cycles divided by scheduled eligible asset-cycles",
                "eligible population",
                "On-time count divided by eligible scheduled count, multiplied by 100; unmeasurable while any selected cycle is active or future."),
            new PmAnalyticsV3MeasureDefinition(
                AssignmentCompletedWithinCount, "Completed within Inspector period", "integer", "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "matching schedules; assignment-period counts use only known initial assignment timestamps",
                "Count first server-accepted completions at or before 30 calendar days after the first saved Inspector assignment. Original PM compliance is separate; reassignment never resets the clock."),
            new PmAnalyticsV3MeasureDefinition(
                AssignmentCompletedLateCount, "Completed after Inspector period", "integer", "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "matching schedules; assignment-period counts use only known initial assignment timestamps",
                "Count first server-accepted completions after 30 calendar days from the first saved Inspector assignment. Original PM compliance is separate; reassignment never resets the clock."),
            new PmAnalyticsV3MeasureDefinition(
                AssignmentOverdueCount, "Inspector period overdue and unfinished", "integer", "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "matching schedules; assignment-period counts use only known initial assignment timestamps",
                "Count unfinished schedules after the original 30-calendar-day Inspector assignment due instant. Original PM compliance is separate; reassignment never resets the clock."),
            new PmAnalyticsV3MeasureDefinition(
                AssignmentOpenCount, "Inspector period open and unfinished", "integer", "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "matching schedules; assignment-period counts use only known initial assignment timestamps",
                "Count unfinished schedules at or before the original 30-calendar-day Inspector assignment due instant. Original PM compliance is separate; reassignment never resets the clock."),
            new PmAnalyticsV3MeasureDefinition(
                AssignmentUnknownCount, "Inspector assignment time unavailable", "integer", "asset-cycles",
                "one eligible persisted schedule for one asset and PM cycle",
                "matching schedules; assignment-period counts use only known initial assignment timestamps",
                "Count schedules without a reliable first saved Inspector assignment timestamp. These are excluded from assignment-period denominators. Original PM compliance is separate; reassignment never resets the clock."),
            new PmAnalyticsV3MeasureDefinition(
                DistinctAssetCount,
                "Distinct assets",
                "integer",
                "assets",
                "unique asset IDs in the stated eligible population",
                "eligible population",
                "Count unique assets in the stated population; this measure is non-additive across PM cycles or groups.")
        });

    internal static IReadOnlyList<PmAnalyticsV3DimensionDefinition> Dimensions { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3DimensionDefinition(
                AssetCategory,
                "Asset category",
                "string",
                "not null for supported CPMP rows",
                "asset",
                "The canonical asset category selected from the supported CPMP catalog."),
            new PmAnalyticsV3DimensionDefinition(
                Department,
                "Department",
                "string",
                "null values form an explicit unknown group",
                "current asset",
                "The asset's current department; historical department snapshots are unavailable."),
            new PmAnalyticsV3DimensionDefinition(
                Building,
                "Building",
                "string",
                "null values form an explicit unknown group",
                "current asset",
                "The asset's current building; historical building snapshots are unavailable."),
            new PmAnalyticsV3DimensionDefinition(
                PmCycle,
                "PM cycle",
                "yyyy-MM string",
                "eligible rows have a stored or explicitly derived cycle",
                "asset-cycle",
                "The scheduled preventive-maintenance cycle; blank stored cycles use the V2 ScheduleDate fallback."),
            new PmAnalyticsV3DimensionDefinition(
                CalendarQuarter,
                "Calendar quarter",
                "year-quarter string",
                "eligible rows have a PM cycle",
                "asset-cycle",
                "Calendar quarter derived from the PM cycle, including its year."),
            new PmAnalyticsV3DimensionDefinition(
                CalendarYear,
                "Calendar year",
                "integer",
                "eligible rows have a PM cycle",
                "asset-cycle",
                "Calendar year derived from the PM cycle.")
        });

    internal static IReadOnlyList<PmAnalyticsV3MeasureDefinition> InventoryMeasures { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3MeasureDefinition(
                RegisteredAssetCount,
                "Registered assets",
                "integer",
                "assets",
                "one registered Asset row per unique asset ID",
                "filtered registered-asset population",
                "Count unique registered asset IDs after category and population filters. The query reads Assets directly and does not join schedules, inspections, forms, acknowledgements, or referral history.")
        });

    internal static IReadOnlyList<PmAnalyticsV3DimensionDefinition> InventoryDimensions { get; } =
        Array.AsReadOnly(new[]
        {
            Dimensions.Single(item => item.Id == AssetCategory),
            Dimensions.Single(item => item.Id == Department),
            Dimensions.Single(item => item.Id == Building),
            new PmAnalyticsV3DimensionDefinition(
                Location,
                "General location",
                "string",
                "null or blank values form an explicit unknown group",
                "current asset",
                "The asset's current general location; historical location snapshots are unavailable."),
            new PmAnalyticsV3DimensionDefinition(
                RegistrationStatus,
                "Registration status",
                "string",
                "persisted catalog status",
                "current asset",
                "The stored asset registration status code. This is separate from inspection condition.")
        });

    internal static IReadOnlyList<PmAnalyticsV3FilterFieldDefinition> FilterFields { get; } =
        Array.AsReadOnly(new[]
        {
            FilterField(Department, "Department", isBase: true, "string", 256, allowNull: true),
            FilterField(Building, "Building", isBase: true, "string", 256, allowNull: true),
            FilterField(Location, "General location", isBase: true, "string", 256, allowNull: true),
            FilterField(AssetCode, "Asset code", isBase: true, "string", 128),
            FilterField(AssetId, "Asset ID", isBase: true, "guid", 36),
            FilterField(Condition, "Inspection condition", isBase: false, "condition", 24, allowedValues: ["Operational", "NonOperational", "NotInspected"]),
            FilterField(Timeliness, "Timeliness", isBase: false, "timeliness", 16, allowedValues: ["OnTime", "Late", "Remaining", "Missed", "Future"]),
            FilterField(ScheduleStatus, "Schedule status", isBase: false, "scheduleStatus", 16, allowedValues: ScheduleStatusCatalog.PersistedValues.Where(value => value != ScheduleStatusCatalog.Cancelled).ToArray()),
            FilterField(AssignmentPeriodStatus, "Inspector assignment period", isBase: false, "assignmentPeriodStatus", 32, allowedValues: ["Unavailable", "WithinPeriod", "Overdue", "CompletedWithinPeriod", "CompletedLate"]),
            FilterField(FormStatus, "Form status", isBase: false, "formStatus", 16, allowNull: true, allowedValues: PreventiveMaintenanceFormStatusCatalog.PersistedValues)
        });

    internal static IReadOnlyList<PmAnalyticsV3FilterFieldDefinition> InventoryFilterFields { get; } =
        Array.AsReadOnly(new[]
        {
            FilterField(Department, "Department", isBase: true, "string", 256, allowNull: true),
            FilterField(Building, "Building", isBase: true, "string", 256, allowNull: true),
            FilterField(Location, "General location", isBase: true, "string", 256, allowNull: true),
            FilterField(
                RegistrationStatus,
                "Registration status",
                isBase: true,
                "registrationStatus",
                16,
                allowedValues: AssetStatusCatalog.PersistedValues)
        });

    internal static IReadOnlyList<PmAnalyticsV3RecordColumnDefinition> RecordColumns { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3RecordColumnDefinition("scheduleId", "Schedule ID", "guid", "", null),
            new PmAnalyticsV3RecordColumnDefinition(AssetId, "Asset ID", "guid", "", null),
            new PmAnalyticsV3RecordColumnDefinition(AssetCode, "Asset code", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(AssetCategory, "Asset category", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(Department, "Current department", "string", "", "May be unknown; current asset value."),
            new PmAnalyticsV3RecordColumnDefinition(Building, "Current building", "string", "", "May be unknown; current asset value."),
            new PmAnalyticsV3RecordColumnDefinition(Location, "General location", "string", "", "May be unknown; current asset value."),
            new PmAnalyticsV3RecordColumnDefinition(PmCycle, "PM cycle", "string", "yyyy-MM", null),
            new PmAnalyticsV3RecordColumnDefinition(Deadline, "Deadline", "date-time", "Asia/Manila", null),
            new PmAnalyticsV3RecordColumnDefinition(
                CompletedAt,
                "Server-recorded completion time",
                "date-time",
                "UTC",
                "Time recorded on the first server-accepted save, separate from the entered inspection date; it does not verify when field work occurred. Null when no completion timestamp is recorded."),
            new PmAnalyticsV3RecordColumnDefinition(ScheduleStatus, "Schedule status", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(FormStatus, "Form status", "string", "", "Null when no linked form is recorded."),
            new PmAnalyticsV3RecordColumnDefinition(Condition, "Inspection condition", "string", "", "NotInspected when there is no completion timestamp."),
            new PmAnalyticsV3RecordColumnDefinition(Timeliness, "Timeliness", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(FirstSupervisorAssignedAt, "First Supervisor assignment", "date-time", "UTC", "Unknown for legacy schedules; never inferred from UpdatedAt."),
            new PmAnalyticsV3RecordColumnDefinition(FirstInspectorAssignedAt, "First Inspector assignment", "date-time", "UTC", "Unknown for legacy schedules; reassignment does not replace this timestamp."),
            new PmAnalyticsV3RecordColumnDefinition(InspectorAssignmentDueAt, "Inspector assignment due", "date-time", "UTC", "First saved Inspector assignment plus 30 calendar days, separate from the PM deadline. Null for unknown initial assignments."),
            new PmAnalyticsV3RecordColumnDefinition(AssignmentPeriodStatus, "Inspector assignment period", "string", "", "Uses first server-accepted completion; original PM compliance remains separate."),
            new PmAnalyticsV3RecordColumnDefinition(UsedLegacyDateFallback, "Used legacy cycle fallback", "boolean", "", null)
        });

    internal static IReadOnlyList<PmAnalyticsV3RecordColumnDefinition> InventoryRecordColumns { get; } =
        Array.AsReadOnly(new[]
        {
            new PmAnalyticsV3RecordColumnDefinition(AssetId, "Asset ID", "guid", "", null),
            new PmAnalyticsV3RecordColumnDefinition(AssetCode, "Asset code", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(AssetCategory, "Asset category", "string", "", null),
            new PmAnalyticsV3RecordColumnDefinition(Department, "Current department", "string", "", "Current Asset value; may be unknown."),
            new PmAnalyticsV3RecordColumnDefinition(Building, "Current building", "string", "", "Current Asset value; may be unknown."),
            new PmAnalyticsV3RecordColumnDefinition(Location, "General location", "string", "", "Current Asset value; may be unknown."),
            new PmAnalyticsV3RecordColumnDefinition(RegistrationStatus, "Registration status", "string", "", "Stored registration status; not inspection condition.")
        });

    internal static IReadOnlyList<string> ClarificationFields { get; } = Array.AsReadOnly(new[]
    {
        "Subject", "Categories", "Period", "Measures", "Mode", "GroupBy", "Presentation", "Department", "Building", "Location", "AssetCode", "AssetId", "Filters", "Thresholds", "Top", "Columns"
    });

    internal static bool IsSubject(string? id) =>
        id is not null && Subjects.Any(subject => string.Equals(subject.Id, id, StringComparison.Ordinal));

    internal static IReadOnlyList<PmAnalyticsV3MeasureDefinition> MeasuresFor(string subject) =>
        subject == SubjectAssetInventory ? InventoryMeasures : Measures;

    internal static IReadOnlyList<PmAnalyticsV3DimensionDefinition> DimensionsFor(string subject) =>
        subject == SubjectAssetInventory ? InventoryDimensions : Dimensions;

    internal static IReadOnlyList<PmAnalyticsV3FilterFieldDefinition> FilterFieldsFor(string subject) =>
        subject == SubjectAssetInventory ? InventoryFilterFields : FilterFields;

    internal static IReadOnlyList<PmAnalyticsV3RecordColumnDefinition> RecordColumnsFor(string subject) =>
        subject == SubjectAssetInventory ? InventoryRecordColumns : RecordColumns;

    internal static bool IsMeasure(string? id, string subject = SubjectPmExecution) =>
        id is not null && MeasuresFor(subject).Any(measure => string.Equals(measure.Id, id, StringComparison.Ordinal));

    internal static bool IsDimension(string? id, string subject = SubjectPmExecution) =>
        id is not null && DimensionsFor(subject).Any(dimension => string.Equals(dimension.Id, id, StringComparison.Ordinal));

    internal static bool IsCategory(string? id) =>
        id is not null && Categories.Any(category => string.Equals(category.Id, id, StringComparison.Ordinal));

    internal static bool IsRecordColumn(string? id, string subject = SubjectPmExecution) =>
        id is not null && RecordColumnsFor(subject).Any(column => string.Equals(column.Id, id, StringComparison.Ordinal));

    internal static PmAnalyticsV3FilterFieldDefinition? FindFilterField(
        string? id,
        string subject = SubjectPmExecution) =>
        id is null
            ? null
            : FilterFieldsFor(subject).FirstOrDefault(field => string.Equals(field.Id, id, StringComparison.Ordinal));

    internal static PmAnalyticsV3RecordColumnDefinition? FindRecordColumn(
        string? id,
        string subject = SubjectPmExecution) =>
        id is null
            ? null
            : RecordColumnsFor(subject).FirstOrDefault(column => string.Equals(column.Id, id, StringComparison.Ordinal));

    private static PmAnalyticsV3FilterFieldDefinition FilterField(
        string id,
        string label,
        bool isBase,
        string valueType,
        int maximumLength,
        bool allowNull = false,
        IReadOnlyList<string>? allowedValues = null)
    {
        var operators = new HashSet<PmAnalyticsV3FilterOperator>
        {
            PmAnalyticsV3FilterOperator.Equal,
            PmAnalyticsV3FilterOperator.In
        };
        if (allowNull)
        {
            operators.Add(PmAnalyticsV3FilterOperator.IsNull);
        }

        return new PmAnalyticsV3FilterFieldDefinition(
            id,
            label,
            isBase,
            valueType,
            maximumLength,
            Array.AsReadOnly((allowedValues ?? []).ToArray()),
            operators);
    }
}
