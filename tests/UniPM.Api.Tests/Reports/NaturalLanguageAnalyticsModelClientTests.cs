using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class NaturalLanguageAnalyticsModelClientTests
{
    private const string ValidOutput = """
        {"status":"Valid","plan":{"metric":"Progress","assetCategory":"fire-extinguisher","pmCycle":"2026-11","department":null,"groupBy":"None"},"clarificationFields":[],"presentation":"Percent"}
        """;

    [Fact]
    public async Task Gemini_uses_fixed_structured_output_request_and_normalizes_usage()
    {
        var handler = new StubHandler("""
            {
              "candidates": [{ "content": { "parts": [{ "text": "__OUTPUT__" }] } }],
              "usageMetadata": {
                "promptTokenCount": 100,
                "candidatesTokenCount": 10,
                "thoughtsTokenCount": 2,
                "cachedContentTokenCount": 20
              },
              "modelVersion": "gemini-3.8-flash-002"
            }
            """.Replace("\"__OUTPUT__\"", JsonSerializer.Serialize(ValidOutput), StringComparison.Ordinal));
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new GeminiNaturalLanguageAnalyticsModelClient("synthetic-key", ledger, handler);

        var response = await client.GenerateAsync(
            "Show progress for fire-extinguisher in 2026-11",
            CancellationToken.None);

        Assert.Equal("Gemini", client.Provider);
        Assert.Equal("gemini-3.8-flash", client.ModelId);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent", handler.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("synthetic-key", handler.GoogleApiKey);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var generationConfig = request.RootElement.GetProperty("generationConfig");
        Assert.Equal(1024, generationConfig.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("application/json", generationConfig.GetProperty("responseMimeType").GetString());
        Assert.Equal("low", generationConfig.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.True(generationConfig.TryGetProperty("responseJsonSchema", out _));
        Assert.False(generationConfig.TryGetProperty("temperature", out _));
        Assert.False(generationConfig.TryGetProperty("topP", out _));
        Assert.False(generationConfig.TryGetProperty("topK", out _));

        Assert.Equal(ValidOutput, response.Content);
        Assert.Equal(100L, response.Usage?.PromptTokens);
        Assert.Equal(12L, response.Usage?.CompletionTokens);
        Assert.Equal(20L, response.Usage?.CachedPromptTokens);
        Assert.Equal(80L, response.Usage?.CacheMissPromptTokens);
        Assert.Equal(2L, response.Usage?.ReasoningTokens);
        Assert.True(response.Usage?.DurationNanoseconds is > 0);

        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Equal(0, snapshot.Retries);
        Assert.Equal(1, snapshot.HttpSuccessResponses);
        var call = Assert.Single(snapshot.Calls);
        Assert.Equal("gemini-3.8-flash-002", call.ReportedModelVersion);
        Assert.Null(call.SystemFingerprint);
    }

    [Fact]
    public async Task Gemini_marks_billed_usage_unavailable_when_thought_count_is_missing()
    {
        var handler = new StubHandler("""
            {
              "candidates": [{ "content": { "parts": [{ "text": "__OUTPUT__" }] } }],
              "usageMetadata": {
                "promptTokenCount": 7,
                "candidatesTokenCount": 2,
                "cachedContentTokenCount": 8
              }
            }
            """.Replace("\"__OUTPUT__\"", JsonSerializer.Serialize(ValidOutput), StringComparison.Ordinal));
        using var client = new GeminiNaturalLanguageAnalyticsModelClient(
            "synthetic-key",
            new NaturalLanguageAnalyticsModelClientRunLedger(),
            handler);

        var response = await client.GenerateAsync("synthetic question", CancellationToken.None);

        Assert.Equal(7L, response.Usage?.PromptTokens);
        Assert.Null(response.Usage?.CompletionTokens);
        Assert.Null(response.Usage?.ReasoningTokens);
        Assert.Null(response.Usage?.CachedPromptTokens);
        Assert.Null(response.Usage?.CacheMissPromptTokens);
    }

    [Fact]
    public async Task DeepSeek_uses_fixed_json_mode_and_reports_cache_and_reasoning_usage()
    {
        var handler = new StubHandler("""
            {
              "model": "deepseek-flash",
              "system_fingerprint": "fp-test_001",
              "choices": [{ "message": { "content": "__OUTPUT__" } }],
              "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 12,
                "prompt_cache_hit_tokens": 25,
                "prompt_cache_miss_tokens": 75,
                "completion_tokens_details": { "reasoning_tokens": 3 }
              }
            }
            """.Replace("\"__OUTPUT__\"", JsonSerializer.Serialize(ValidOutput), StringComparison.Ordinal));
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient("synthetic-key", ledger, handler);

        var response = await client.GenerateAsync("Show progress for fire-extinguisher in 2026-11", CancellationToken.None);

        Assert.Equal("DeepSeek", client.Provider);
        Assert.Equal("deepseek-flash", client.ModelId);
        Assert.Equal("https://api.deepseek.com/chat/completions", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("synthetic-key", handler.AuthorizationParameter);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var root = request.RootElement;
        Assert.Equal("deepseek-flash", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(1024, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.Equal("disabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());

        Assert.Equal(100L, response.Usage?.PromptTokens);
        Assert.Equal(12L, response.Usage?.CompletionTokens);
        Assert.Equal(25L, response.Usage?.CachedPromptTokens);
        Assert.Equal(75L, response.Usage?.CacheMissPromptTokens);
        Assert.Equal(3L, response.Usage?.ReasoningTokens);
        var call = Assert.Single(ledger.Snapshot().Calls);
        Assert.Equal("deepseek-flash", call.ReportedModelVersion);
        Assert.Equal("fp-test_001", call.SystemFingerprint);
    }

    [Fact]
    public async Task DeepSeek_discards_inconsistent_cache_buckets()
    {
        var handler = new StubHandler("""
            {
              "choices": [{ "message": { "content": "__OUTPUT__" } }],
              "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 12,
                "prompt_cache_hit_tokens": 25,
                "prompt_cache_miss_tokens": 74
              }
            }
            """.Replace("\"__OUTPUT__\"", JsonSerializer.Serialize(ValidOutput), StringComparison.Ordinal));
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient(
            "synthetic-key",
            new NaturalLanguageAnalyticsModelClientRunLedger(),
            handler);

        var response = await client.GenerateAsync("synthetic question", CancellationToken.None);

        Assert.Equal(100L, response.Usage?.PromptTokens);
        Assert.Null(response.Usage?.CachedPromptTokens);
        Assert.Null(response.Usage?.CacheMissPromptTokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Http400RequestRejected")]
    [InlineData(HttpStatusCode.Unauthorized, "Http401AuthenticationRejected")]
    [InlineData(HttpStatusCode.PaymentRequired, "Http402BillingRejected")]
    [InlineData(HttpStatusCode.Forbidden, "Http403PermissionDenied")]
    [InlineData(HttpStatusCode.NotFound, "Http404NotFound")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Http422ValidationRejected")]
    public async Task Permanent_provider_failure_stops_without_retry_and_keeps_error_safe(
        HttpStatusCode statusCode,
        string safeCode)
    {
        var handler = new StubHandler("provider body must not be surfaced", statusCode);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient("synthetic-key", ledger, handler);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));
        var secondException = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable, exception.Failure);
        Assert.Equal(exception.Message, secondException.Message);
        Assert.DoesNotContain("provider body", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Equal(0, snapshot.Retries);
        Assert.Equal(1, snapshot.ProviderFailures);
        Assert.Equal(safeCode, snapshot.TerminalFailureCode);
        Assert.Equal((int)statusCode, Assert.Single(snapshot.Calls).HttpStatusCode);
    }

    [Fact]
    public async Task Transient_statuses_retry_twice_then_succeed_with_separate_call_and_attempt_counts()
    {
        var delays = new List<TimeSpan>();
        var handler = new ScriptedHandler(
            () => ErrorResponse(HttpStatusCode.RequestTimeout),
            () => ErrorResponse(HttpStatusCode.TooManyRequests),
            SuccessResponse);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger, delays.Add);

        var response = await client.GenerateAsync("synthetic question", CancellationToken.None);

        Assert.Equal(ValidOutput, response.Content);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, delays);
        Assert.Equal(3, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(3, snapshot.Attempts);
        Assert.Equal(2, snapshot.Retries);
        Assert.Equal(2, snapshot.ProviderFailures);
        Assert.Null(snapshot.TerminalFailureCode);
        Assert.Equal(new int?[] { 408, 429, 200 }, snapshot.Calls.Select(call => call.HttpStatusCode));
        Assert.Equal(new[] { "Http408RequestTimeout", "Http429RateLimited", null },
            snapshot.Calls.Select(call => call.SafeErrorCode));
    }

    [Fact]
    public async Task Permanent_status_on_http_exception_is_classified_without_retry()
    {
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException(
                "private response details",
                inner: null,
                statusCode: HttpStatusCode.Forbidden),
            SuccessResponse);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable, exception.Failure);
        Assert.DoesNotContain("private response details", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Equal(0, snapshot.Retries);
        Assert.Equal("Http403PermissionDenied", snapshot.TerminalFailureCode);
        Assert.Equal(403, Assert.Single(snapshot.Calls).HttpStatusCode);
    }

    [Fact]
    public async Task Exhausted_transient_retries_stop_later_logical_calls()
    {
        var handler = new ScriptedHandler(
            () => ErrorResponse(HttpStatusCode.ServiceUnavailable),
            () => ErrorResponse(HttpStatusCode.ServiceUnavailable),
            () => ErrorResponse(HttpStatusCode.ServiceUnavailable));
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));
        var stoppedException = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("another synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable, exception.Failure);
        Assert.Equal(exception.Message, stoppedException.Message);
        Assert.Equal(3, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(3, snapshot.Attempts);
        Assert.Equal(2, snapshot.Retries);
        Assert.Equal(3, snapshot.ProviderFailures);
        Assert.Equal("Http5xxServerError", snapshot.TerminalFailureCode);
    }

    [Fact]
    public async Task Retry_after_within_bound_is_honored()
    {
        var delays = new List<TimeSpan>();
        var handler = new ScriptedHandler(
            () => ErrorResponse(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(3)),
            SuccessResponse);
        using var client = CreateDeepSeekClient(handler, new NaturalLanguageAnalyticsModelClientRunLedger(), delays.Add);

        await client.GenerateAsync("synthetic question", CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromSeconds(3) }, delays);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Retry_after_above_bound_stops_without_retrying_early()
    {
        var delays = new List<TimeSpan>();
        var handler = new ScriptedHandler(
            () => ErrorResponse(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(6)),
            SuccessResponse);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger, delays.Add);

        await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Empty(delays);
        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal("RetryAfterExceedsBound", snapshot.TerminalFailureCode);
        Assert.Equal("Http429RateLimited", Assert.Single(snapshot.Calls).SafeErrorCode);
        Assert.Equal(429, Assert.Single(snapshot.Calls).HttpStatusCode);
    }

    [Fact]
    public async Task Transport_failure_retries_without_exposing_exception_text()
    {
        var delays = new List<TimeSpan>();
        var handler = new ScriptedHandler(
            () => throw new HttpRequestException("private transport details"),
            SuccessResponse);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger, delays.Add);

        var response = await client.GenerateAsync("synthetic question", CancellationToken.None);

        Assert.Equal(ValidOutput, response.Content);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, delays);
        Assert.Equal(2, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(2, snapshot.Attempts);
        Assert.Equal(1, snapshot.Retries);
        Assert.Equal("TransportUnavailable", snapshot.Calls[0].SafeErrorCode);
        Assert.DoesNotContain("private transport details", snapshot.Calls[0].SafeErrorCode!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Caller_cancellation_during_retry_wait_stops_without_another_attempt()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new ScriptedHandler(() => ErrorResponse(HttpStatusCode.ServiceUnavailable));
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient(
            "synthetic-key",
            ledger,
            handler,
            (delay, token) =>
            {
                cancellation.Cancel();
                return Task.Delay(delay, token);
            },
            () => 0);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GenerateAsync("synthetic question", cancellation.Token));

        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Equal(0, snapshot.Retries);
        Assert.Equal("CallerCancelled", snapshot.TerminalFailureCode);
    }

    [Fact]
    public async Task Immediate_provider_timeout_is_recorded_without_waiting()
    {
        var handler = new ImmediateTimeoutHandler();
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.Timeout, exception.Failure);
        Assert.Equal(3, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(3, snapshot.Attempts);
        Assert.Equal(2, snapshot.Retries);
        Assert.Equal(3, snapshot.ProviderFailures);
        Assert.Equal("TransportTimeout", snapshot.TerminalFailureCode);
    }

    [Fact]
    public async Task Interpretation_rejection_keeps_usage_from_the_successful_provider_response()
    {
        var responseBody = JsonSerializer.Serialize(new
        {
            model = "deepseek-flash",
            choices = new[] { new { message = new { content = "not-json" } } },
            usage = new { prompt_tokens = 30, completion_tokens = 7 }
        });
        var handler = new StubHandler(responseBody);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient("synthetic-key", ledger, handler);

        var response = await client.GenerateAsync("synthetic question", CancellationToken.None);
        var exception = Assert.Throws<NaturalLanguageAnalyticsProviderException>(() =>
            NaturalLanguageAnalyticsInterpretationOutput.Parse(response.Content, response.Usage));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.InvalidOutput, exception.Failure);
        Assert.Equal(30L, response.Usage?.PromptTokens);
        Assert.Equal(7L, response.Usage?.CompletionTokens);
        var call = Assert.Single(ledger.Snapshot().Calls);
        Assert.Equal(30L, call.Usage?.PromptTokens);
        Assert.Equal(7L, call.Usage?.CompletionTokens);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(1, ledger.Snapshot().Attempts);
        Assert.Equal(0, ledger.Snapshot().Retries);
    }

    [Fact]
    public async Task Malformed_successful_provider_envelope_is_not_retried()
    {
        var handler = new ScriptedHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json")
            },
            SuccessResponse);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = CreateDeepSeekClient(handler, ledger);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.InvalidOutput, exception.Failure);
        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.LogicalCalls);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Equal(0, snapshot.Retries);
        Assert.Equal(200, Assert.Single(snapshot.Calls).HttpStatusCode);
    }

    [Fact]
    public async Task Oversized_provider_response_is_rejected_and_not_retained()
    {
        var handler = new StubHandler(new string('x', NaturalLanguageAnalyticsApiModelClientBase.MaximumResponseBytes + 1));
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var client = new DeepSeekNaturalLanguageAnalyticsModelClient("synthetic-key", ledger, handler);

        var exception = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            client.GenerateAsync("synthetic question", CancellationToken.None));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.InvalidOutput, exception.Failure);
        Assert.DoesNotContain("xxxx", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);
        var snapshot = ledger.Snapshot();
        Assert.Equal(1, snapshot.HttpSuccessResponses);
        Assert.Null(Assert.Single(snapshot.Calls).Usage);
    }

    [Theory]
    [InlineData("{\"status\":\"Unsupported\",\"status\":\"Valid\",\"plan\":null,\"clarificationFields\":[],\"presentation\":null}")]
    [InlineData("{\"status\":\"Unsupported\",\"plan\":{\"metric\":\"Progress\",\"metric\":\"NonOperational\"},\"clarificationFields\":[],\"presentation\":null}")]
    public void Interpretation_output_rejects_duplicate_properties(string json)
    {
        var exception = Assert.Throws<NaturalLanguageAnalyticsProviderException>(() =>
            NaturalLanguageAnalyticsInterpretationOutput.Parse(json, null));

        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.InvalidOutput, exception.Failure);
    }

    [Theory]
    [InlineData("qwen3:4b-instruct", "qwen3:4b-instruct")]
    [InlineData("model\nsecret", null)]
    [InlineData("mødel", null)]
    [InlineData("", null)]
    public void Ledger_metadata_accepts_only_bounded_ascii_identifiers(string value, string? expected)
        => Assert.Equal(expected, NaturalLanguageAnalyticsApiModelClientBase.SafeMetadata(value));

    private static DeepSeekNaturalLanguageAnalyticsModelClient CreateDeepSeekClient(
        HttpMessageHandler handler,
        NaturalLanguageAnalyticsModelClientRunLedger ledger,
        Action<TimeSpan>? recordDelay = null)
        => new(
            "synthetic-key",
            ledger,
            handler,
            (delay, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                recordDelay?.Invoke(delay);
                return Task.CompletedTask;
            },
            () => 0);

    private static HttpResponseMessage ErrorResponse(
        HttpStatusCode statusCode,
        TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("provider error body is not retained", Encoding.UTF8, "application/json")
        };
        if (retryAfter is TimeSpan delay)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
        }

        return response;
    }

    private static HttpResponseMessage SuccessResponse()
    {
        var envelope = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = ValidOutput } } },
            usage = new { prompt_tokens = 10, completion_tokens = 5 }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = responses[RequestCount++]();
            return Task.FromResult(response);
        }
    }

    private sealed class StubHandler(
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        internal Uri? RequestUri { get; private set; }

        internal HttpMethod? RequestMethod { get; private set; }

        internal string? RequestBody { get; private set; }

        internal string? GoogleApiKey { get; private set; }

        internal string? AuthorizationScheme { get; private set; }

        internal string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestMethod = request.Method;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            GoogleApiKey = request.Headers.TryGetValues("x-goog-api-key", out var values)
                ? values.Single()
                : null;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ImmediateTimeoutHandler : HttpMessageHandler
    {
        internal int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException());
        }
    }
}
