using System.Net.Http.Headers;
using System.Text.Json;

namespace UniPM.Api.Features.Reports;

internal sealed class DeepSeekNaturalLanguageAnalyticsModelClient(
    string apiKey,
    NaturalLanguageAnalyticsModelClientRunLedger ledger,
    HttpMessageHandler? handler = null,
    Func<TimeSpan, CancellationToken, Task>? retryDelay = null,
    Func<int>? retryJitterMilliseconds = null,
    string? modelId = null)
    : NaturalLanguageAnalyticsApiModelClientBase(
        apiKey,
        ledger,
        handler,
        retryDelay,
        retryJitterMilliseconds)
{
    internal const string ConfiguredModelId = "deepseek-flash";
    internal const string V4ProModelId = "deepseek-v4-pro";
    internal const int GenerationTemperature = 0;
    internal const string ApiKeyEnvironmentVariable = "DEEPSEEK_API_KEY";

    private readonly string _modelId = ResolveModelId(modelId);
    private static readonly Uri ChatCompletionsEndpoint = new(
        "https://api.deepseek.com/chat/completions",
        UriKind.Absolute);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override string Provider => "DeepSeek";

    public override string ModelId => _modelId;

    protected override Uri Endpoint => ChatCompletionsEndpoint;

    protected override byte[] CreateRequestBody(string sanitizedQuestion)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            model = _modelId,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = NaturalLanguageAnalyticsInterpretationPrompt.SystemPrompt
                },
                new { role = "user", content = sanitizedQuestion }
            },
            stream = false,
            max_tokens = MaximumOutputTokens,
            temperature = GenerationTemperature,
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" }
        }, JsonOptions);

    protected override NaturalLanguageAnalyticsModelResponse ParseSuccessfulResponse(
        byte[] responseBody,
        long durationNanoseconds)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var usageElement = GetObject(root, "usage");
        var promptTokens = GetLong(usageElement, "prompt_tokens");
        var completionTokens = GetLong(usageElement, "completion_tokens");
        var cacheHitTokens = GetLong(usageElement, "prompt_cache_hit_tokens");
        var cacheMissTokens = GetLong(usageElement, "prompt_cache_miss_tokens");
        if (!promptTokens.HasValue
            || !cacheHitTokens.HasValue
            || !cacheMissTokens.HasValue
            || cacheHitTokens.Value > promptTokens.Value
            || cacheMissTokens.Value != promptTokens.Value - cacheHitTokens.Value)
        {
            cacheHitTokens = null;
            cacheMissTokens = null;
        }

        var reasoningTokens = GetLong(
            GetObject(usageElement, "completion_tokens_details"),
            "reasoning_tokens");
        var usage = new NaturalLanguageAnalyticsProviderUsage(
            promptTokens,
            completionTokens,
            durationNanoseconds,
            cacheHitTokens,
            cacheMissTokens,
            reasoningTokens);

        var content = string.Empty;
        if (GetArray(root, "choices") is { } choices
            && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (GetObject(choice, "message") is { } message)
            {
                content = GetString(message, "content") ?? string.Empty;
            }
        }

        return new NaturalLanguageAnalyticsModelResponse(
            content,
            usage,
            GetString(root, "model"),
            GetString(root, "system_fingerprint"));
    }

    protected override void SetAuthentication(HttpRequestMessage request, string apiKey)
        => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    private static string ResolveModelId(string? modelId)
    {
        var selected = modelId ?? ConfiguredModelId;
        return selected is ConfiguredModelId or V4ProModelId
            ? selected
            : throw new ArgumentException("Unsupported DeepSeek model.", nameof(modelId));
    }

    private static JsonElement? GetObject(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Object
            ? property
            : null;

    private static JsonElement? GetArray(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value
            : null;

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetLong(JsonElement? parent, string name)
        => parent is { ValueKind: JsonValueKind.Object } value
            && value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var count)
            && count >= 0
            ? count
            : null;
}
