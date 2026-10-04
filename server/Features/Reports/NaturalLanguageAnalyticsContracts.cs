using System.Text.Json.Serialization;

namespace UniPM.Api.Features.Reports;

internal interface INaturalLanguageAnalyticsInterpreter
{
    Task<PmAnalyticsInterpretationResult> InterpretAsync(
        string question,
        CancellationToken cancellationToken);
}

internal enum PmAnalyticsInterpretationStatus
{
    Valid,
    NeedsClarification,
    Unsupported
}

internal enum PmAnalyticsClarificationField
{
    Metric,
    AssetCategory,
    Year,
    Month,
    Department,
    GroupBy
}

internal enum PmAnalyticsPresentation
{
    Count,
    Percent
}

internal sealed record PmAnalyticsInterpretationResult(
    PmAnalyticsInterpretationStatus Status,
    PmAnalyticsPlan? Plan,
    IReadOnlyList<PmAnalyticsClarificationField> ClarificationFields,
    PmAnalyticsPresentation? Presentation,
    string? Code,
    NaturalLanguageAnalyticsProviderUsage? Usage = null);

internal sealed record NaturalLanguageAnalyticsProviderUsage(
    long? PromptTokens,
    long? CompletionTokens,
    long? DurationNanoseconds);

internal enum NaturalLanguageAnalyticsProviderFailure
{
    ProviderUnavailable,
    Timeout,
    InvalidOutput
}

internal sealed class NaturalLanguageAnalyticsProviderException(
    NaturalLanguageAnalyticsProviderFailure failure)
    : Exception("The natural language analytics interpreter is unavailable.")
{
    internal NaturalLanguageAnalyticsProviderFailure Failure { get; } = failure;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PmAnalyticsInterpretationRequest(string? Question);

public sealed record PmAnalyticsInterpretationResponse(
    string Status,
    PmAnalyticsPlanResponse? Plan,
    IReadOnlyList<string> ClarificationFields,
    string? Presentation,
    string? Code,
    string? CanonicalQuestion);
