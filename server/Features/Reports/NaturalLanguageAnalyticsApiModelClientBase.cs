using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UniPM.Api.Features.Reports;

internal abstract class NaturalLanguageAnalyticsApiModelClientBase : INaturalLanguageAnalyticsModelClient, IDisposable
{
    internal const int MaximumOutputTokens = 1024;
    internal const int MaximumResponseBytes = 16 * 1024;
    internal const int TimeoutSeconds = 60;
    internal const int MaximumRetries = NaturalLanguageAnalyticsModelClientRunLedger.MaximumRetriesPerLogicalCall;
    internal const int MaximumRetryAfterSeconds = 5;
    internal const string RetryPolicyVersion = "bounded-transient-v1";
    internal const int FirstRetryBackoffSeconds = 1;
    internal const int SecondRetryBackoffSeconds = 2;
    internal const int MinimumJitterMilliseconds = 0;
    internal const int MaximumJitterMilliseconds = 250;

    private const int MaximumRequestBytes = 16 * 1024;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly NaturalLanguageAnalyticsModelClientRunLedger _ledger;
    private readonly Func<TimeSpan, CancellationToken, Task> _retryDelay;
    private readonly Func<int> _retryJitterMilliseconds;

    protected NaturalLanguageAnalyticsApiModelClientBase(
        string apiKey,
        NaturalLanguageAnalyticsModelClientRunLedger ledger,
        HttpMessageHandler? handler,
        Func<TimeSpan, CancellationToken, Task>? retryDelay = null,
        Func<int>? retryJitterMilliseconds = null)
    {
        _apiKey = apiKey;
        _ledger = ledger;
        _retryDelay = retryDelay ?? ((delay, token) => Task.Delay(delay, token));
        _retryJitterMilliseconds = retryJitterMilliseconds
            ?? (() => Random.Shared.Next(MinimumJitterMilliseconds, MaximumJitterMilliseconds + 1));
        _httpClient = new HttpClient(handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public abstract string Provider { get; }

    public abstract string ModelId { get; }

    protected abstract Uri Endpoint { get; }

    protected abstract byte[] CreateRequestBody(string sanitizedQuestion);

    protected abstract NaturalLanguageAnalyticsModelResponse ParseSuccessfulResponse(
        byte[] responseBody,
        long durationNanoseconds);

    protected abstract void SetAuthentication(HttpRequestMessage request, string apiKey);

    public async Task<NaturalLanguageAnalyticsModelResponse> GenerateAsync(
        string sanitizedQuestion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey)
            || _apiKey.Length > 4096
            || _apiKey.Any(char.IsControl))
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }

        var requestBody = CreateRequestBody(sanitizedQuestion);
        if (requestBody.Length > MaximumRequestBytes)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        _ledger.BeginLogicalCall();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        for (var attemptNumber = 1; attemptNumber <= MaximumRetries + 1; attemptNumber++)
        {
            _ledger.BeginAttempt(isRetry: attemptNumber > 1);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new ByteArrayContent(requestBody)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            SetAuthentication(request, _apiKey);

            var stopwatch = Stopwatch.StartNew();
            var successfulStatus = false;
            var responseRecorded = false;
            var attemptFailureRecorded = false;
            int? httpStatusCode = null;

            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                httpStatusCode = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    stopwatch.Stop();
                    var statusCode = response.StatusCode;
                    var safeCode = SafeStatusCode(statusCode);
                    var retryable = false;
                    var exceededRetryAfterBound = false;
                    var retryDelay = TimeSpan.Zero;
                    if (IsTransientStatus(statusCode) && attemptNumber <= MaximumRetries)
                    {
                        retryable = TryCreateRetryDelay(
                            attemptNumber,
                            GetRetryAfter(response.Headers),
                            out retryDelay,
                            out exceededRetryAfterBound);
                    }

                    _ledger.RecordProviderFailure(
                        safeCode,
                        stopwatch.Elapsed.TotalMilliseconds,
                        stopRun: !retryable,
                        terminalCode: exceededRetryAfterBound ? "RetryAfterExceedsBound" : null,
                        httpStatusCode: httpStatusCode);
                    attemptFailureRecorded = true;

                    if (!retryable)
                    {
                        throw new NaturalLanguageAnalyticsProviderException(
                            NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
                    }

                    await _retryDelay(retryDelay, timeout.Token);
                    continue;
                }

                successfulStatus = true;
                var body = await ReadBoundedAsync(response.Content, MaximumResponseBytes, timeout.Token);
                stopwatch.Stop();

                NaturalLanguageAnalyticsModelResponse parsed;
                try
                {
                    parsed = ParseSuccessfulResponse(body, stopwatch.Elapsed.Ticks * 100L);
                }
                catch (JsonException)
                {
                    _ledger.RecordHttpSuccess(
                        null,
                        hasText: false,
                        stopwatch.Elapsed.TotalMilliseconds,
                        reportedModelVersion: null,
                        systemFingerprint: null,
                        httpStatusCode: httpStatusCode);
                    responseRecorded = true;
                    throw new NaturalLanguageAnalyticsProviderException(
                        NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
                }

                _ledger.RecordHttpSuccess(
                    parsed.Usage,
                    !string.IsNullOrWhiteSpace(parsed.Content),
                    stopwatch.Elapsed.TotalMilliseconds,
                    SafeMetadata(parsed.ReportedModelVersion),
                    SafeMetadata(parsed.SystemFingerprint),
                    httpStatusCode: httpStatusCode);
                responseRecorded = true;
                return parsed;
            }
            catch (NaturalLanguageAnalyticsProviderException)
            {
                if (successfulStatus && !responseRecorded)
                {
                    stopwatch.Stop();
                    _ledger.RecordHttpSuccess(
                        null,
                        hasText: false,
                        stopwatch.Elapsed.TotalMilliseconds,
                        reportedModelVersion: null,
                        systemFingerprint: null,
                        httpStatusCode: httpStatusCode);
                }

                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                if (attemptFailureRecorded)
                {
                    _ledger.MarkTerminalFailure("CallerCancelled");
                }
                else
                {
                    _ledger.RecordProviderFailure(
                        "CallerCancelled",
                        stopwatch.Elapsed.TotalMilliseconds,
                        stopRun: true,
                        httpStatusCode: httpStatusCode);
                }

                throw;
            }
            catch (OperationCanceledException) when (!timeout.IsCancellationRequested)
            {
                stopwatch.Stop();
                var retryable = attemptNumber <= MaximumRetries;
                _ledger.RecordProviderFailure(
                    "TransportTimeout",
                    stopwatch.Elapsed.TotalMilliseconds,
                    stopRun: !retryable,
                    httpStatusCode: httpStatusCode);
                attemptFailureRecorded = true;
                if (!retryable)
                {
                    throw new NaturalLanguageAnalyticsProviderException(
                        NaturalLanguageAnalyticsProviderFailure.Timeout);
                }

                try
                {
                    await _retryDelay(CreateBackoffDelay(attemptNumber), timeout.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _ledger.MarkTerminalFailure("CallerCancelled");
                    throw;
                }
                catch (OperationCanceledException)
                {
                    _ledger.MarkTerminalFailure("ProviderTimeout");
                    throw new NaturalLanguageAnalyticsProviderException(
                        NaturalLanguageAnalyticsProviderFailure.Timeout);
                }

                continue;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                if (attemptFailureRecorded)
                {
                    _ledger.MarkTerminalFailure("ProviderTimeout");
                }
                else
                {
                    _ledger.RecordProviderFailure(
                        "ProviderTimeout",
                        stopwatch.Elapsed.TotalMilliseconds,
                        stopRun: true,
                        httpStatusCode: httpStatusCode);
                }

                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.Timeout);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                stopwatch.Stop();
                var exceptionStatus = (exception as HttpRequestException)?.StatusCode;
                var observedStatusCode = exceptionStatus is null ? httpStatusCode : (int)exceptionStatus.Value;
                var safeCode = exceptionStatus is null
                    ? "TransportUnavailable"
                    : SafeStatusCode(exceptionStatus.Value);
                var retryable = attemptNumber <= MaximumRetries
                    && (exceptionStatus is null || IsTransientStatus(exceptionStatus.Value));
                _ledger.RecordProviderFailure(
                    safeCode,
                    stopwatch.Elapsed.TotalMilliseconds,
                    stopRun: !retryable,
                    httpStatusCode: observedStatusCode);
                attemptFailureRecorded = true;
                if (!retryable)
                {
                    throw new NaturalLanguageAnalyticsProviderException(
                        NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
                }

                try
                {
                    await _retryDelay(CreateBackoffDelay(attemptNumber), timeout.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _ledger.MarkTerminalFailure("CallerCancelled");
                    throw;
                }
                catch (OperationCanceledException)
                {
                    _ledger.MarkTerminalFailure("ProviderTimeout");
                    throw new NaturalLanguageAnalyticsProviderException(
                        NaturalLanguageAnalyticsProviderFailure.Timeout);
                }

                continue;
            }
        }

        throw new NaturalLanguageAnalyticsProviderException(
            NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
    }

    public void Dispose() => _httpClient.Dispose();

    internal static string SafeStatusCode(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return status switch
        {
            400 => "Http400RequestRejected",
            401 => "Http401AuthenticationRejected",
            402 => "Http402BillingRejected",
            403 => "Http403PermissionDenied",
            404 => "Http404NotFound",
            408 => "Http408RequestTimeout",
            422 => "Http422ValidationRejected",
            429 => "Http429RateLimited",
            >= 500 and <= 599 => "Http5xxServerError",
            >= 400 and <= 499 => "Http4xxRequestRejected",
            _ => "HttpStatusRejected"
        };
    }

    internal static bool IsTransientStatus(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return status == 408 || status == 429 || status is >= 500 and <= 599;
    }

    private bool TryCreateRetryDelay(
        int retryNumber,
        RetryConditionHeaderValue? retryAfter,
        out TimeSpan delay,
        out bool retryAfterExceededBound)
    {
        retryAfterExceededBound = false;
        var backoff = CreateBackoffDelay(retryNumber);
        var requestedDelay = retryAfter?.Delta;
        if (requestedDelay is null && retryAfter?.Date is DateTimeOffset retryAt)
        {
            requestedDelay = retryAt - DateTimeOffset.UtcNow;
        }

        if (requestedDelay is TimeSpan serverDelay)
        {
            if (serverDelay > TimeSpan.FromSeconds(MaximumRetryAfterSeconds))
            {
                retryAfterExceededBound = true;
                delay = TimeSpan.Zero;
                return false;
            }

            if (serverDelay < TimeSpan.Zero)
            {
                serverDelay = TimeSpan.Zero;
            }

            if (serverDelay > backoff)
            {
                backoff = serverDelay;
            }
        }

        delay = backoff;
        return true;
    }

    private TimeSpan CreateBackoffDelay(int retryNumber)
    {
        var seconds = retryNumber == 1 ? FirstRetryBackoffSeconds : SecondRetryBackoffSeconds;
        var jitter = Math.Clamp(
            _retryJitterMilliseconds(),
            MinimumJitterMilliseconds,
            MaximumJitterMilliseconds);
        return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(jitter);
    }

    private static RetryConditionHeaderValue? GetRetryAfter(HttpResponseHeaders headers)
    {
        try
        {
            return headers.RetryAfter;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal static string? SafeMetadata(string? value)
        => value is not null
            && value.Length is > 0 and <= 128
            && value.All(character =>
                character is >= 'A' and <= 'Z'
                    or >= 'a' and <= 'z'
                    or >= '0' and <= '9'
                    or '.' or '_' or ':' or '-')
            ? value
            : null;

    internal static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > maximumBytes)
        {
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(Math.Min(maximumBytes, 4096));
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return output.ToArray();
            }

            if (output.Length + read > maximumBytes)
            {
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
            }

            output.Write(buffer, 0, read);
        }
    }
}
