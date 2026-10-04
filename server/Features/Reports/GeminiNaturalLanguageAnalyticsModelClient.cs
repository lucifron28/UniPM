using System.Text.Json;

namespace UniPM.Api.Features.Reports;

internal sealed class GeminiNaturalLanguageAnalyticsModelClient(
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
    internal const string ConfiguredModelId = "gemini-3.8-flash";
    internal const string FlashLiteModelId = "gemini-3.5-flash-lite";
    internal const string ThinkingLevel = "low";
    internal const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    private readonly string _modelId = ResolveModelId(modelId);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override string Provider => "Gemini";

    public override string ModelId => _modelId;

    protected override Uri Endpoint => CreateEndpoint(_modelId);

    protected override byte[] CreateRequestBody(string sanitizedQuestion)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = NaturalLanguageAnalyticsInterpretationPrompt.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = sanitizedQuestion } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = MaximumOutputTokens,
                responseMimeType = "application/json",
                responseJsonSchema = NaturalLanguageAnalyticsInterpretationPrompt.OutputSchema,
                thinkingConfig = new { thinkingLevel = ThinkingLevel }
            }
        }, JsonOptions);

    protected override NaturalLanguageAnalyticsModelResponse ParseSuccessfulResponse(
        byte[] responseBody,
        long durationNanoseconds)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var usageMetadata = GetObject(root, "usageMetadata");
        var promptTokens = GetLong(usageMetadata, "promptTokenCount");
        var candidateTokens = GetLong(usageMetadata, "candidatesTokenCount");
        var reasoningTokens = GetLong(usageMetadata, "thoughtsTokenCount");
        long? completionTokens = candidateTokens.HasValue
            && reasoningTokens.HasValue
            && candidateTokens.Value <= long.MaxValue - reasoningTokens.Value
                ? candidateTokens.Value + reasoningTokens.Value
                : null;
        var cachedTokens = GetLong(usageMetadata, "cachedContentTokenCount");
        long? cacheMissTokens = promptTokens.HasValue && cachedTokens.HasValue
            && cachedTokens.Value <= promptTokens.Value
            ? promptTokens.Value - cachedTokens.Value
            : null;
        if (!cacheMissTokens.HasValue)
        {
            cachedTokens = null;
        }
        var usage = new NaturalLanguageAnalyticsProviderUsage(
            promptTokens,
            completionTokens,
            durationNanoseconds,
            cachedTokens,
            cacheMissTokens,
            reasoningTokens);

        var content = string.Empty;
        if (GetArray(root, "candidates") is { } candidates
            && candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (GetObject(candidate, "content") is { } responseContent
                && GetArray(responseContent, "parts") is { } parts)
            {
                content = string.Concat(parts.EnumerateArray()
                    .Where(part => !IsThought(part))
                    .Select(part => GetString(part, "text"))
                    .Where(text => text is not null));
            }
        }

        return new NaturalLanguageAnalyticsModelResponse(
            content,
            usage,
            GetString(root, "modelVersion"));
    }

    protected override void SetAuthentication(HttpRequestMessage request, string apiKey)
        => request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

    private static string ResolveModelId(string? modelId)
    {
        var selected = modelId ?? ConfiguredModelId;
        return selected is ConfiguredModelId or FlashLiteModelId
            ? selected
            : throw new ArgumentException("Unsupported Gemini model.", nameof(modelId));
    }

    private static Uri CreateEndpoint(string selectedModelId)
        => new(
            $"https://generativelanguage.googleapis.com/v1beta/models/{selectedModelId}:generateContent",
            UriKind.Absolute);

    private static bool IsThought(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("thought", out var thought)
            && thought.ValueKind == JsonValueKind.True;

    private static JsonElement? GetObject(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
            ? value
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
