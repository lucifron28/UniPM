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

    private const int MaximumRequestBytes = 16 * 1024;
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly NaturalLanguageAnalyticsModelClientRunLedger _ledger;

    protected NaturalLanguageAnalyticsApiModelClientBase(
        string apiKey,
        NaturalLanguageAnalyticsModelClientRunLedger ledger,
        HttpMessageHandler? handler)
    {
        _apiKey = apiKey;
        _ledger = ledger;
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

        _ledger.BeginAttempt();
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new ByteArrayContent(requestBody)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        SetAuthentication(request, _apiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        var successfulStatus = false;
        var responseRecorded = false;

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                _ledger.RecordProviderFailure(
                    SafeStatusCode(response.StatusCode),
                    stopwatch.Elapsed.TotalMilliseconds,
                    stopRun: true);
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
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
                    systemFingerprint: null);
                responseRecorded = true;
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
            }

            _ledger.RecordHttpSuccess(
                parsed.Usage,
                !string.IsNullOrWhiteSpace(parsed.Content),
                stopwatch.Elapsed.TotalMilliseconds,
                SafeMetadata(parsed.ReportedModelVersion),
                SafeMetadata(parsed.SystemFingerprint));
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
                    systemFingerprint: null);
            }

            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _ledger.RecordProviderFailure("ProviderTimeout", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            _ledger.RecordProviderFailure("ProviderUnavailable", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }
        catch (IOException)
        {
            stopwatch.Stop();
            _ledger.RecordProviderFailure("ProviderUnavailable", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private static string SafeStatusCode(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return status switch
        {
            400 or 422 => "ProviderRequestRejected",
            401 or 403 => "ProviderAuthenticationRejected",
            402 or 429 => "ProviderQuotaOrBillingRejected",
            >= 500 => "ProviderUnavailable",
            _ => "ProviderRequestRejected"
        };
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
