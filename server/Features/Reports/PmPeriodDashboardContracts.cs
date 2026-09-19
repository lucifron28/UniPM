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
    bool ComplianceMeasurable,
    int Scheduled,
    int Inspected,
    int CompletedOnTime,
    int CompletedLate,
    int NotCompleted,
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
    int CompletedLate,
    int NotCompleted,
    Guid? FormId,
    string? FormStatus,
    string? FileNumber,
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

internal static class PmPeriodDashboardFilterCatalog
{
    internal const string Operational = "Operational";
    internal const string NonOperational = "NonOperational";
    internal const string NotInspected = "NotInspected";

    internal const string OnTime = "OnTime";
    internal const string Late = "Late";
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
            "notcompleted" or "not-completed" or "pending" => NotCompleted,
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
