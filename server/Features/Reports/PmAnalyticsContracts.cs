using System.Text.Json.Serialization;

namespace UniPM.Api.Features.Reports;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsQuestionRequest(string? Question);

internal enum PmAnalyticsMetric
{
    Progress,
    OnTimeCompliance,
    CompletedLate,
    NonOperational
}

internal enum PmAnalyticsGroupBy
{
    None,
    Department
}

internal sealed record PmAnalyticsPlan(
    PmAnalyticsMetric Metric,
    string AssetCategory,
    string PmCycle,
    string? Department,
    PmAnalyticsGroupBy GroupBy);

public sealed record PmAnalyticsPlanResponse(
    string Metric,
    string AssetCategory,
    string PmCycle,
    string? Department,
    string GroupBy);

public sealed record PmAnalyticsMeasureResponse(
    string? Department,
    int Numerator,
    int Denominator,
    decimal? Value,
    string Unit,
    bool IsMeasurable);

public sealed record PmAnalyticsSourceResponse(
    Guid ScheduleId,
    Guid AssetId,
    Guid? InspectionId,
    string AssetCode,
    string? Department,
    string PmCycle,
    DateTimeOffset Deadline,
    DateTimeOffset? InspectionCompletedAt,
    string Timeliness,
    string Condition,
    string? FormStatus);

public sealed record PmAnalyticsResponse(
    PmAnalyticsPlanResponse Plan,
    DateTimeOffset Deadline,
    string PeriodState,
    PmAnalyticsMeasureResponse Result,
    IReadOnlyList<PmAnalyticsMeasureResponse> Groups,
    IReadOnlyList<PmAnalyticsSourceResponse> Sources,
    int TotalSourceCount,
    bool SourcesTruncated,
    string ScopeNote);

internal sealed record PmAnalyticsExecutionResult(
    PmAnalyticsResponse? Response,
    string? ValidationError);
