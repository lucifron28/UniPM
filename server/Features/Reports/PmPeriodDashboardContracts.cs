namespace UniPM.Api.Features.Reports;

public sealed record PmPeriodDashboardCycleGroupResponse(
    string AssetCategory,
    int Year,
    IReadOnlyList<PmPeriodDashboardCycleOptionResponse> Cycles);

public sealed record PmPeriodDashboardCycleOptionResponse(
    string PmCycle,
    int Scheduled);

public sealed record PmPeriodDashboardResponse(
    string PmCycle,
    string AssetCategory,
    string? Department,
    DateTimeOffset Deadline,
    string PeriodState,
    bool ComplianceMeasurable,
    bool InspectionResultsAvailable,
    int Scheduled,
    int Inspected,
    int CompletedOnTime,
    int CompletedLate,
    int NotCompleted,
    int Remaining,
    int Operational,
    int NonOperational,
    decimal? OnTimeCompliancePercent,
    decimal ProgressPercent,
    IReadOnlyList<PmPeriodDashboardBatchResponse> Batches,
    IReadOnlyList<PmPeriodDashboardAssetRowResponse> Assets);

public sealed record PmPeriodDashboardBatchResponse(
    string? Department,
    string AssetCategory,
    string PmCycle,
    int Scheduled,
    int Inspected,
    int CompletedOnTime,
    decimal? OnTimeCompliancePercent,
    int CompletedLate,
    int NotCompleted,
    int Remaining,
    Guid? FormId,
    string? FormStatus,
    string? FileNumber,
    DateTimeOffset? FieldWorkCompletedAt,
    DateTimeOffset? SubmittedAt,
    bool IsAcknowledged,
    DateTimeOffset? AcknowledgedAt);

public sealed record PmPeriodDashboardAssetRowResponse(
    Guid ScheduleId,
    Guid AssetId,
    Guid? InspectionId,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Location,
    string? Department,
    string PmCycle,
    DateTimeOffset ScheduleDate,
    DateTimeOffset Deadline,
    string ScheduleStatus,
    string ExecutionStatus,
    bool IsInspected,
    DateTimeOffset? InspectionCompletedAt,
    string Timeliness,
    string Condition,
    string? Remarks,
    string? ActionsRecommendations,
    Guid? FormId,
    string? FormStatus,
    bool IsAcknowledged,
    DateTimeOffset? AcknowledgedAt);

internal sealed record PmPeriodDashboardQuery(
    string PmCycle,
    string AssetCategory,
    string? Department,
    string? Condition,
    string? Timeliness,
    string? Search);

internal static class PmPeriodDashboardPeriodStateCatalog
{
    internal const string Future = "Future";
    internal const string Active = "Active";
    internal const string Closed = "Closed";
}

internal static class PmPeriodDashboardFilterCatalog
{
    internal const string Operational = "Operational";
    internal const string NonOperational = "NonOperational";
    internal const string NotInspected = "NotInspected";

    internal const string OnTime = "OnTime";
    internal const string Late = "Late";
    internal const string Scheduled = "Scheduled";
    internal const string Pending = "Pending";
    internal const string NotCompleted = "NotCompleted";

    internal static bool TryNormalizeCondition(string? value, out string normalized)
    {
        normalized = string.Empty;
        var candidate = NormalizeToken(value);
        normalized = candidate switch
        {
            "operational" => Operational,
            "nonoperational" or "non-operational" => NonOperational,
            "notinspected" or "not-inspected" or "uninspected" or "un-inspected" => NotInspected,
            _ => string.Empty
        };

        return normalized.Length > 0;
    }

    internal static bool TryNormalizeTimeliness(string? value, out string normalized)
    {
        normalized = string.Empty;
        var candidate = NormalizeToken(value);
        normalized = candidate switch
        {
            "ontime" or "completedontime" or "completed-on-time" => OnTime,
            "late" or "completedlate" or "completed-late" => Late,
            "scheduled" => Scheduled,
            "pending" or "remaining" => Pending,
            "notcompleted" or "not-completed" => NotCompleted,
            _ => string.Empty
        };

        return normalized.Length > 0;
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
