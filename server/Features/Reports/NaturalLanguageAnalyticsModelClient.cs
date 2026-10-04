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
    internal const int MaximumRequests = 60;

    private readonly object _sync = new();
    private readonly List<NaturalLanguageAnalyticsModelCallObservation> _observations = [];
    private int _attempts;
    private int _httpSuccessResponses;
    private int _responsesWithText;
    private int _providerFailures;
    private string? _terminalFailureCode;

    internal void BeginAttempt()
    {
        lock (_sync)
        {
            if (_terminalFailureCode is not null)
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            if (_attempts >= MaximumRequests)
            {
                _terminalFailureCode = "RequestBudgetExhausted";
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            _attempts++;
        }
    }

    internal void RecordHttpSuccess(
        NaturalLanguageAnalyticsProviderUsage? usage,
        bool hasText,
        double latencyMilliseconds,
        string? reportedModelVersion,
        string? systemFingerprint)
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
                null));
        }
    }

    internal void RecordProviderFailure(
        string safeCode,
        double latencyMilliseconds,
        bool stopRun)
    {
        lock (_sync)
        {
            _providerFailures++;
            if (stopRun)
            {
                _terminalFailureCode ??= safeCode;
            }

            _observations.Add(new NaturalLanguageAnalyticsModelCallObservation(
                false,
                false,
                null,
                Math.Round(latencyMilliseconds, 2),
                null,
                null,
                safeCode));
        }
    }

    internal NaturalLanguageAnalyticsModelClientRunSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new NaturalLanguageAnalyticsModelClientRunSnapshot(
                _attempts,
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
    string? SafeErrorCode);

internal sealed record NaturalLanguageAnalyticsModelClientRunSnapshot(
    int Attempts,
    int HttpSuccessResponses,
    int ResponsesWithText,
    int ProviderFailures,
    string? TerminalFailureCode,
    IReadOnlyList<NaturalLanguageAnalyticsModelCallObservation> Calls);
