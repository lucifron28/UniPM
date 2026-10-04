using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Reports;

internal sealed class OllamaNaturalLanguageAnalyticsModelClient(
    HttpClient httpClient,
    IOptionsMonitor<NaturalLanguageAnalyticsOptions> options,
    NaturalLanguageAnalyticsModelClientRunLedger? ledger = null)
    : INaturalLanguageAnalyticsModelClient
{
    private const int MaximumCallsPerProcess = 100;
    private const int HardMaximumResponseBytes = 16 * 1024;
    private static int _providerCalls;

    public string Provider => "Ollama";

    public string ModelId => options.CurrentValue.Model;

    public async Task<NaturalLanguageAnalyticsModelResponse> GenerateAsync(
        string sanitizedQuestion,
        CancellationToken cancellationToken)
    {
        var configuration = options.CurrentValue;
        var endpoint = ValidateOptions(configuration);
        if (endpoint is null)
        {
            ledger?.RecordProviderFailure("ProviderUnavailable", 0, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }

        if (Interlocked.Increment(ref _providerCalls) > MaximumCallsPerProcess)
        {
            ledger?.RecordProviderFailure("RequestBudgetExhausted", 0, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }

        ledger?.BeginAttempt();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(CreateRequest(configuration, sanitizedQuestion)),
                Encoding.UTF8,
                "application/json")
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        var successfulStatus = false;
        var responseRecorded = false;

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                ledger?.RecordProviderFailure(
                    SafeStatusCode(response.StatusCode),
                    stopwatch.Elapsed.TotalMilliseconds,
                    stopRun: true);
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
            }

            successfulStatus = true;
            var body = await NaturalLanguageAnalyticsApiModelClientBase.ReadBoundedAsync(
                response.Content,
                configuration.MaxResponseBytes,
                timeout.Token);
            stopwatch.Stop();

            NaturalLanguageAnalyticsModelResponse parsed;
            try
            {
                parsed = ParseResponse(body);
            }
            catch (JsonException)
            {
                ledger?.RecordHttpSuccess(
                    null,
                    hasText: false,
                    latencyMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    reportedModelVersion: null,
                    systemFingerprint: null);
                responseRecorded = true;
                throw new NaturalLanguageAnalyticsProviderException(
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput);
            }

            ledger?.RecordHttpSuccess(
                parsed.Usage,
                !string.IsNullOrWhiteSpace(parsed.Content),
                stopwatch.Elapsed.TotalMilliseconds,
                NaturalLanguageAnalyticsApiModelClientBase.SafeMetadata(parsed.ReportedModelVersion),
                NaturalLanguageAnalyticsApiModelClientBase.SafeMetadata(parsed.SystemFingerprint));
            responseRecorded = true;
            return parsed;
        }
        catch (NaturalLanguageAnalyticsProviderException)
        {
            if (successfulStatus && !responseRecorded)
            {
                stopwatch.Stop();
                ledger?.RecordHttpSuccess(
                    null,
                    hasText: false,
                    latencyMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
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
            ledger?.RecordProviderFailure("ProviderTimeout", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            ledger?.RecordProviderFailure("ProviderUnavailable", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }
        catch (IOException)
        {
            stopwatch.Stop();
            ledger?.RecordProviderFailure("ProviderUnavailable", stopwatch.Elapsed.TotalMilliseconds, stopRun: true);
            throw new NaturalLanguageAnalyticsProviderException(
                NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable);
        }
    }

    private static object CreateRequest(
        NaturalLanguageAnalyticsOptions configuration,
        string question)
    {
        return new
        {
            model = configuration.Model,
            stream = false,
            think = false,
            format = NaturalLanguageAnalyticsInterpretationPrompt.OutputSchema,
            options = new
            {
                num_predict = configuration.MaxOutputTokens,
                temperature = OllamaNaturalLanguageAnalyticsInterpreter.GenerationTemperature,
                seed = OllamaNaturalLanguageAnalyticsInterpreter.GenerationSeed,
                num_ctx = OllamaNaturalLanguageAnalyticsInterpreter.ContextTokens
            },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = NaturalLanguageAnalyticsInterpretationPrompt.SystemPrompt
                },
                new { role = "user", content = question }
            }
        };
    }

    private static NaturalLanguageAnalyticsModelResponse ParseResponse(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var message = GetObject(root, "message");
        var content = message is { } responseMessage
            ? GetString(responseMessage, "content") ?? string.Empty
            : string.Empty;
        var usage = new NaturalLanguageAnalyticsProviderUsage(
            GetLong(root, "prompt_eval_count"),
            GetLong(root, "eval_count"),
            GetLong(root, "total_duration"));

        return new NaturalLanguageAnalyticsModelResponse(
            content,
            usage,
            GetString(root, "model"));
    }

    private static Uri? ValidateOptions(NaturalLanguageAnalyticsOptions configuration)
    {
        if (!configuration.Enabled
            || configuration.TimeoutSeconds is < 1 or > 60
            || configuration.MaxOutputTokens is < 1 or > NaturalLanguageAnalyticsApiModelClientBase.MaximumOutputTokens
            || configuration.MaxResponseBytes is < 1 or > HardMaximumResponseBytes
            || string.IsNullOrWhiteSpace(configuration.Model)
            || configuration.Model.Length > 100
            || configuration.Model.Any(character => !(char.IsLetterOrDigit(character)
                || character is '-' or '_' or '.' or ':' or '/'))
            || !Uri.TryCreate(configuration.BaseAddress, UriKind.Absolute, out var baseAddress)
            || !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || !baseAddress.IsLoopback
            || !string.IsNullOrEmpty(baseAddress.UserInfo)
            || baseAddress.AbsolutePath != "/"
            || !string.IsNullOrEmpty(baseAddress.Query)
            || !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            return null;
        }

        return new Uri(baseAddress, "api/chat");
    }

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

    private static JsonElement? GetObject(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Object
            ? property
            : null;

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long? GetLong(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var value)
            && value >= 0
            ? value
            : null;
}
