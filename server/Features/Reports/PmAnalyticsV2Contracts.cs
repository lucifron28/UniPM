using System.Text.Json.Serialization;

namespace UniPM.Api.Features.Reports;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2InterpretRequest(string? Question);

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2Metric>))]
public enum PmAnalyticsV2Metric
{
    Progress,
    OnTimeCompliance,
    CompletedLate,
    NonOperational,
    Remaining,
    Missed
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2PeriodKind>))]
public enum PmAnalyticsV2PeriodKind
{
    Month,
    Quarter,
    Year,
    Range
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2Presentation>))]
public enum PmAnalyticsV2Presentation
{
    Count,
    Percent,
    Records
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2CountBasis>))]
public enum PmAnalyticsV2CountBasis
{
    AssetCycles,
    DistinctAssets
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2SourceScope>))]
public enum PmAnalyticsV2SourceScope
{
    Matching,
    Calculation
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2GroupBy>))]
public enum PmAnalyticsV2GroupBy
{
    None,
    Department,
    Building,
    PmCycle
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2Direction>))]
public enum PmAnalyticsV2Direction
{
    Highest,
    Lowest
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2Condition>))]
public enum PmAnalyticsV2Condition
{
    Operational,
    NonOperational,
    NotInspected
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV2Timeliness>))]
public enum PmAnalyticsV2Timeliness
{
    OnTime,
    Late,
    Remaining,
    Missed,
    Future
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2Period(
    [property: JsonRequired]
    PmAnalyticsV2PeriodKind Kind,
    int? Year = null,
    int? Month = null,
    int? Quarter = null,
    string? StartCycle = null,
    string? EndCycle = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2Comparison(string EarlierCycle, string LaterCycle);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2Ranking(
    int Limit,
    [property: JsonRequired]
    PmAnalyticsV2Direction Direction);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2Plan(
    [property: JsonRequired]
    int SchemaVersion,
    [property: JsonRequired]
    PmAnalyticsV2Metric Metric,
    [property: JsonRequired]
    string AssetCategory,
    PmAnalyticsV2Period? Period,
    PmAnalyticsV2Comparison? Comparison,
    [property: JsonRequired]
    PmAnalyticsV2Presentation Presentation,
    [property: JsonRequired]
    PmAnalyticsV2CountBasis CountBasis,
    string? Department,
    string? Building,
    string? AssetCode,
    PmAnalyticsV2Condition? Condition,
    PmAnalyticsV2Timeliness? Timeliness,
    [property: JsonRequired]
    PmAnalyticsV2GroupBy GroupBy,
    PmAnalyticsV2Ranking? Ranking);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV2QueryRequest(
    PmAnalyticsV2Plan? Plan,
    PmAnalyticsV2SourceScope SourceScope = PmAnalyticsV2SourceScope.Matching,
    int Page = 1,
    int PageSize = 25);

public sealed record PmAnalyticsV2InterpretationResponse(
    string Status,
    PmAnalyticsV2Plan? Plan,
    IReadOnlyList<string> ClarificationFields,
    string? Code,
    string? Detail);

public sealed record PmAnalyticsV2MeasureResponse(
    int Numerator,
    int Denominator,
    decimal? Value,
    string Unit,
    bool IsMeasurable,
    string? UnmeasurableReason);

public sealed record PmAnalyticsV2PopulationResponse(
    int EligibleAssetCycleCount,
    int EligibleDistinctAssetCount,
    int MatchingAssetCycleCount,
    int MatchingDistinctAssetCount,
    int SourceTotalCount,
    int TotalPages);

public sealed record PmAnalyticsV2CycleResponse(
    string PmCycle,
    DateTimeOffset Deadline,
    string State,
    int EligibleAssetCycleCount,
    int EligibleDistinctAssetCount,
    int InspectedCount,
    int CompletedOnTimeCount,
    int CompletedLateCount,
    int NonOperationalCount,
    int RemainingCount,
    int MissedCount,
    int LegacyDateFallbackCount);

public sealed record PmAnalyticsV2GroupResponse(
    PmAnalyticsV2GroupBy GroupBy,
    string? Value,
    PmAnalyticsV2MeasureResponse Result,
    int EligibleAssetCycleCount,
    int EligibleDistinctAssetCount,
    int MatchingAssetCycleCount,
    int MatchingDistinctAssetCount);

public sealed record PmAnalyticsV2RankingExclusionResponse(
    string? Department,
    string Reason);

public sealed record PmAnalyticsV2ComparisonResponse(
    string EarlierCycle,
    string LaterCycle,
    PmAnalyticsV2MeasureResponse Earlier,
    PmAnalyticsV2MeasureResponse Later,
    decimal? Delta,
    string DeltaUnit);

public sealed record PmAnalyticsV2SourceResponse(
    Guid ScheduleId,
    Guid AssetId,
    Guid? InspectionId,
    string AssetCode,
    string AssetCategory,
    string? CurrentDepartment,
    string? CurrentBuilding,
    string PmCycle,
    DateTimeOffset Deadline,
    DateTimeOffset? CompletedAt,
    string ScheduleStatus,
    string? FormStatus,
    string Condition,
    string Timeliness,
    bool UsedLegacyDateFallback);

public sealed record PmAnalyticsV2QueryResponse(
    int SchemaVersion,
    DateTimeOffset AsOfUtc,
    PmAnalyticsV2Plan Plan,
    PmAnalyticsV2SourceScope SourceScope,
    int Page,
    int PageSize,
    PmAnalyticsV2PopulationResponse Population,
    PmAnalyticsV2MeasureResponse? Result,
    IReadOnlyList<PmAnalyticsV2CycleResponse> Cycles,
    IReadOnlyList<PmAnalyticsV2GroupResponse> Groups,
    IReadOnlyList<PmAnalyticsV2RankingExclusionResponse> RankingExclusions,
    PmAnalyticsV2ComparisonResponse? Comparison,
    IReadOnlyList<PmAnalyticsV2SourceResponse> Sources,
    string ScopeNote);
