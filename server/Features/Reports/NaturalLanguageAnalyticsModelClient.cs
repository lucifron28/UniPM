namespace UniPM.Api.Features.Reports;

internal interface INaturalLanguageAnalyticsModelClient
{
    string Provider { get; }

    string ModelId { get; }

    Task<NaturalLanguageAnalyticsModelResponse> GenerateAsync(
        string sanitizedQuestion,
        CancellationToken cancellationToken);
}

internal sealed record NaturalLanguageAnalyticsModelResponse(
    string Content,
    NaturalLanguageAnalyticsProviderUsage? Usage,
    string? ReportedModelVersion = null,
    string? SystemFingerprint = null);

internal sealed class NaturalLanguageAnalyticsModelClientRunLedger
{
    internal const int MaximumLogicalCalls = 60;
    internal const int MaximumRetriesPerLogicalCall = 2;
    internal const int MaximumAttempts = MaximumLogicalCalls * (MaximumRetriesPerLogicalCall + 1);
    internal const int MaximumRequests = MaximumLogicalCalls;

    private readonly object _sync = new();
    private readonly List<NaturalLanguageAnalyticsModelCallObservation> _observations = [];
    private int _logicalCalls;
    private int _attempts;
    private int _retries;
    private int _httpSuccessResponses;
    private int _responsesWithText;
    private int _providerFailures;
    private string? _terminalFailureCode;

    internal void BeginLogicalCall()
    {
        lock (_sync)
        {
            if (_terminalFailureCode is not null)
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            if (_logicalCalls >= MaximumLogicalCalls)
            {
                _terminalFailureCode = "LogicalCallBudgetExhausted";
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            _logicalCalls++;
        }
    }

    internal void BeginAttempt(bool isRetry)
    {
        lock (_sync)
        {
            if (_terminalFailureCode is not null)
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            if (_attempts >= MaximumAttempts)
            {
                _terminalFailureCode = "HttpAttemptBudgetExhausted";
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            if (isRetry)
            {
                _retries++;
            }

            _attempts++;
        }
    }

    internal void BeginAttempt()
    {
        BeginLogicalCall();
        BeginAttempt(isRetry: false);
    }

    internal void RecordHttpSuccess(
        NaturalLanguageAnalyticsProviderUsage? usage,
        bool hasText,
        double latencyMilliseconds,
        string? reportedModelVersion,
        string? systemFingerprint,
        int? httpStatusCode = null)
    {
        lock (_sync)
        {
            _httpSuccessResponses++;
            if (hasText)
            {
                _responsesWithText++;
            }

            _observations.Add(new NaturalLanguageAnalyticsModelCallObservation(
                true,
                hasText,
                usage,
                Math.Round(latencyMilliseconds, 2),
                reportedModelVersion,
                systemFingerprint,
                null,
                httpStatusCode));
        }
    }

    internal void RecordProviderFailure(
        string safeCode,
        double latencyMilliseconds,
        bool stopRun,
        string? terminalCode = null,
        int? httpStatusCode = null)
    {
        lock (_sync)
        {
            _providerFailures++;
            if (stopRun)
            {
                _terminalFailureCode ??= terminalCode ?? safeCode;
            }

            _observations.Add(new NaturalLanguageAnalyticsModelCallObservation(
                false,
                false,
                null,
                Math.Round(latencyMilliseconds, 2),
                null,
                null,
                safeCode,
                httpStatusCode));
        }
    }

    internal void MarkTerminalFailure(string safeCode)
    {
        lock (_sync)
        {
            _terminalFailureCode ??= safeCode;
        }
    }

    internal NaturalLanguageAnalyticsModelClientRunSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new NaturalLanguageAnalyticsModelClientRunSnapshot(
                _logicalCalls,
                _attempts,
                _retries,
                _httpSuccessResponses,
                _responsesWithText,
                _providerFailures,
                _terminalFailureCode,
                _observations.ToArray());
        }
    }
}

internal sealed record NaturalLanguageAnalyticsModelCallObservation(
    bool HttpSucceeded,
    bool HasText,
    NaturalLanguageAnalyticsProviderUsage? Usage,
    double LatencyMilliseconds,
    string? ReportedModelVersion,
    string? SystemFingerprint,
    string? SafeErrorCode,
    int? HttpStatusCode = null);

internal sealed record NaturalLanguageAnalyticsModelClientRunSnapshot(
    int LogicalCalls,
    int Attempts,
    int Retries,
    int HttpSuccessResponses,
    int ResponsesWithText,
    int ProviderFailures,
    string? TerminalFailureCode,
    IReadOnlyList<NaturalLanguageAnalyticsModelCallObservation> Calls);
