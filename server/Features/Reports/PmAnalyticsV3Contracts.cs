using System.Text.Json.Serialization;

namespace UniPM.Api.Features.Reports;

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3Mode>))]
public enum PmAnalyticsV3Mode
{
    Aggregate,
    Records
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3Presentation>))]
public enum PmAnalyticsV3Presentation
{
    Table,
    Summary
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3PeriodKind>))]
public enum PmAnalyticsV3PeriodKind
{
    Month,
    Quarter,
    Year,
    Range
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3FilterOperator>))]
public enum PmAnalyticsV3FilterOperator
{
    Equal,
    In,
    IsNull
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3NumericOperator>))]
public enum PmAnalyticsV3NumericOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3SortDirection>))]
public enum PmAnalyticsV3SortDirection
{
    Ascending,
    Descending
}

[JsonConverter(typeof(JsonStringEnumConverter<PmAnalyticsV3Interpreter>))]
public enum PmAnalyticsV3Interpreter
{
    DeepSeek,
    DeterministicParser
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3Period(
    [property: JsonRequired]
    string Basis,
    [property: JsonRequired]
    PmAnalyticsV3PeriodKind Kind,
    int? Year = null,
    int? Month = null,
    int? Quarter = null,
    string? StartCycle = null,
    string? EndCycle = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3Filter(
    [property: JsonRequired]
    string Field,
    [property: JsonRequired]
    PmAnalyticsV3FilterOperator Operator,
    IReadOnlyList<string>? Values = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3AggregateFilter(
    [property: JsonRequired]
    string Measure,
    [property: JsonRequired]
    PmAnalyticsV3NumericOperator Operator,
    [property: JsonRequired]
    decimal Value);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3OrderBy(
    [property: JsonRequired]
    string Field,
    [property: JsonRequired]
    PmAnalyticsV3SortDirection Direction);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3Top(
    [property: JsonRequired]
    string Measure,
    [property: JsonRequired]
    int Limit,
    [property: JsonRequired]
    PmAnalyticsV3SortDirection Direction);

public sealed record PmAnalyticsV3SubjectDefinition(
    string Id,
    string Label,
    string Description,
    bool RequiresPeriod);

/// <summary>Typed, closed V3 report plan contract.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3ReportPlan(
    [property: JsonRequired]
    int SchemaVersion,
    [property: JsonRequired]
    int CatalogVersion,
    [property: JsonRequired]
    string Subject,
    [property: JsonRequired]
    PmAnalyticsV3Mode Mode,
    [property: JsonRequired]
    PmAnalyticsV3Period? Period,
    [property: JsonRequired]
    IReadOnlyList<string> Categories,
    [property: JsonRequired]
    IReadOnlyList<string> Measures,
    IReadOnlyList<string>? GroupBy,
    [property: JsonRequired]
    IReadOnlyList<PmAnalyticsV3Filter> BaseFilters,
    [property: JsonRequired]
    IReadOnlyList<PmAnalyticsV3Filter> MatchingFilters,
    [property: JsonRequired]
    IReadOnlyList<PmAnalyticsV3AggregateFilter> AggregateFilters,
    [property: JsonRequired]
    IReadOnlyList<PmAnalyticsV3OrderBy> OrderBy,
    PmAnalyticsV3Top? Top,
    [property: JsonRequired]
    PmAnalyticsV3Presentation Presentation,
    [property: JsonRequired]
    IReadOnlyList<string> Columns);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3PreviewRequest(
    [property: JsonRequired]
    PmAnalyticsV3ReportPlan? Plan);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsV3ExecuteRequest(
    [property: JsonRequired]
    string PreviewId,
    int Page = 1,
    int PageSize = 25);

public sealed record PmAnalyticsV3PreviewResponse(
    string PreviewId,
    DateTimeOffset ExpiresAtUtc,
    PmAnalyticsV3ReportPlan Plan,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CyclesByCategory,
    string Summary,
    IReadOnlyList<string> ScopeNotes);

public sealed record PmAnalyticsV3CatalogResponse(
    int SchemaVersion,
    int CatalogVersion,
    string Subject,
    IReadOnlyList<PmAnalyticsV3SubjectDefinition> Subjects,
    IReadOnlyList<PmAnalyticsV3CategoryDefinition> Categories,
    IReadOnlyList<PmAnalyticsV3MeasureDefinition> Measures,
    IReadOnlyList<PmAnalyticsV3DimensionDefinition> Dimensions,
    IReadOnlyList<PmAnalyticsV3FilterFieldDefinition> FilterFields,
    IReadOnlyList<PmAnalyticsV3RecordColumnDefinition> RecordColumns);

public sealed record PmAnalyticsV3FilterValuesResponse(
    string Field,
    IReadOnlyList<string> Values,
    bool HasMore);

public sealed record PmAnalyticsV3Column(
    string Id,
    string Label,
    string ValueType,
    string Unit,
    string? NullSemantics);

public sealed record PmAnalyticsV3MeasureResult(
    decimal? Value,
    int Numerator,
    int Denominator,
    string Unit,
    bool IsMeasurable,
    string? UnmeasurableReason,
    int EligibleRowCount,
    int MatchingRowCount);

public sealed record PmAnalyticsV3Population(
    int EligibleAssetCycleCount,
    int EligibleDistinctAssetCount,
    int MatchingAssetCycleCount,
    int MatchingDistinctAssetCount,
    int SourceTotalCount,
    int GroupsBeforeAggregateFilters,
    int GroupsAfterAggregateFilters,
    int GroupsReturnedAfterTop);

public sealed record PmAnalyticsV3AggregateRow(
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyDictionary<string, PmAnalyticsV3MeasureResult> Measures);

public sealed record PmAnalyticsV3RecordRow(
    Guid? ScheduleId,
    Guid AssetId,
    Guid? InspectionId,
    IReadOnlyDictionary<string, object?> Values);

public sealed record PmAnalyticsV3ExecutionResponse(
    int SchemaVersion,
    int CatalogVersion,
    DateTimeOffset AsOfUtc,
    PmAnalyticsV3ReportPlan Plan,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CyclesByCategory,
    PmAnalyticsV3Mode Mode,
    IReadOnlyList<PmAnalyticsV3Column> Columns,
    PmAnalyticsV3Population Population,
    int Page,
    int PageSize,
    int TotalPages,
    IReadOnlyList<PmAnalyticsV3AggregateRow> AggregateRows,
    IReadOnlyList<PmAnalyticsV3RecordRow> Records,
    IReadOnlyList<string> ScopeNotes)
{
    public IReadOnlyList<PmAnalyticsV3AggregateRow>? ChartRows { get; init; }
    public string? ChartUnavailableReason { get; init; }
}

public sealed record PmAnalyticsV3InterpretRequest(
    [property: JsonRequired]
    string? Question,
    PmAnalyticsV3Interpreter? Interpreter = null);

public sealed record PmAnalyticsV3InterpretationResponse(
    string Status,
    PmAnalyticsV3ReportPlan? Plan,
    IReadOnlyList<string> ClarificationFields,
    string? Code,
    string? Detail,
    string Interpreter);
