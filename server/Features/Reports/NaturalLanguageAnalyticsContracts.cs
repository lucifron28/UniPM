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

// CompletionTokens is total billed output (including any reasoning tokens); ReasoningTokens is a subset.
// Cache counts are both null when the provider omits or inconsistently reports either bucket.
internal sealed record NaturalLanguageAnalyticsProviderUsage(
    long? PromptTokens,
    long? CompletionTokens,
    long? DurationNanoseconds,
    long? CachedPromptTokens = null,
    long? CacheMissPromptTokens = null,
    long? ReasoningTokens = null);

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
