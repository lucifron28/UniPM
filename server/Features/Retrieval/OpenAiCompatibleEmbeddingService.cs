using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.Retrieval;

internal sealed class OpenAiCompatibleEmbeddingService(
    HttpClient httpClient,
    IOptions<EmbeddingOptions> optionsAccessor)
    : IEmbeddingService
{
    private readonly EmbeddingOptions options = optionsAccessor.Value;

    public EmbeddingServiceDescriptor Descriptor
    {
        get
        {
            var providerKey = options.ProviderKey?.Trim() ?? string.Empty;
            var model = options.Model?.Trim() ?? string.Empty;
            var dimensions = options.Dimensions;
            var profile = string.Join(
                ':',
                EmbeddingOptions.ProviderAdapterKey,
                providerKey,
                model,
                MaintenanceEmbeddingInput.InputFormatVersion,
                dimensions?.ToString() ?? "unknown");

            return new EmbeddingServiceDescriptor(
                options.Enabled,
                providerKey,
                model,
                dimensions,
                profile);
        }
    }

    public async Task<IReadOnlyList<EmbeddingVector>> GenerateBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabledAndConfigured();

        if (inputs.Count is < 1 or > 128 || inputs.Count > options.MaxBatchSize)
        {
            throw new EmbeddingVectorValidationException(
                $"Embedding batches must contain between 1 and {options.MaxBatchSize} inputs.");
        }

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new EmbeddingVectorValidationException("Embedding inputs cannot be blank.");
            }

            if (input.Length > options.MaxInputCharacters)
            {
                throw new EmbeddingVectorValidationException(
                    $"Embedding inputs cannot exceed {options.MaxInputCharacters} characters.");
            }
        }

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildRequestUri());
        request.Content = new StringContent(
            JsonSerializer.Serialize(
                new EmbeddingRequest(options.Model!, inputs.ToArray()),
                JsonSerializerOptions.Web),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new EmbeddingServiceExecutionException("The embedding provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new EmbeddingServiceExecutionException(
                "The embedding provider request could not be completed.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EmbeddingServiceExecutionException(
                    $"The embedding provider returned HTTP {(int)response.StatusCode}.");
            }

            try
            {
                await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCancellation.Token);
                using var document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: linkedCancellation.Token);
                var vectors = ParseVectors(document.RootElement, inputs.Count);
                if (options.Dimensions is not null
                    && vectors.Any(vector => vector.Dimensions != options.Dimensions))
                {
                    throw new EmbeddingVectorValidationException(
                        "The embedding provider returned dimensions different from configuration.");
                }

                return vectors;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new EmbeddingServiceExecutionException("The embedding provider response timed out.");
            }
            catch (JsonException exception)
            {
                throw new EmbeddingVectorValidationException(
                    "The embedding provider returned malformed vector data.",
                    exception);
            }
        }
    }

    private void EnsureEnabledAndConfigured()
    {
        if (!options.Enabled)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Semantic embeddings are disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Model must be configured before semantic embedding operations run.");
        }

        if (string.IsNullOrWhiteSpace(options.ProviderKey)
            || options.ProviderKey.Length > 64)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:ProviderKey must be configured as a non-secret value of 64 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseAddress)
            || !Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var baseAddress)
            || baseAddress.Scheme is not ("http" or "https"))
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:BaseAddress must be an absolute HTTP or HTTPS endpoint.");
        }

        if (string.IsNullOrWhiteSpace(options.Path))
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Path must be configured before semantic embedding operations run.");
        }

        if (!Uri.TryCreate(options.Path, UriKind.Relative, out var relativePath)
            || relativePath.IsAbsoluteUri
            || options.Path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Path must be a relative request path.");
        }

        var requestUri = new Uri(baseAddress, relativePath);
        if (!string.Equals(requestUri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(requestUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != baseAddress.Port)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Path must resolve to the configured base endpoint.");
        }

        if (!options.AllowRemoteProvider && !IsLocalEndpoint(requestUri))
        {
            throw new EmbeddingServiceAvailabilityException(
                "Remote embedding endpoints require Embeddings:AllowRemoteProvider=true and a separate privacy review.");
        }

        if (options.TimeoutSeconds is < 1 or > 300
            || options.MaxBatchSize is < 1 or > 128
            || options.MaxInputCharacters is < 1)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embedding timeout, batch, and input limits are outside the supported bounds.");
        }

        if (options.Dimensions is null)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Dimensions must be configured when semantic embeddings are enabled.");
        }

        if (options.Dimensions is < EmbeddingVectorCodec.MinDimensions or > EmbeddingVectorCodec.MaxDimensions)
        {
            throw new EmbeddingServiceAvailabilityException(
                "Embeddings:Dimensions is outside the supported vector dimension range.");
        }
    }

    private Uri BuildRequestUri()
    {
        var baseAddress = new Uri(options.BaseAddress!, UriKind.Absolute);
        return new Uri(baseAddress, options.Path.TrimStart('/'));
    }

    private static bool IsLocalEndpoint(Uri endpoint)
    {
        return endpoint.IsLoopback
            || string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<EmbeddingVector> ParseVectors(
        JsonElement root,
        int expectedCount)
    {
        if (!root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() != expectedCount)
        {
            throw new EmbeddingVectorValidationException(
                "The embedding provider returned an unexpected vector count.");
        }

        var indexedVectors = new (int Index, double[] Values)[expectedCount];
        var usedIndexes = new HashSet<int>();
        var fallbackIndex = 0;
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var embedding)
                || embedding.ValueKind != JsonValueKind.Array)
            {
                throw new EmbeddingVectorValidationException(
                    "The embedding provider returned an invalid vector item.");
            }

            var values = new double[embedding.GetArrayLength()];
            var valueIndex = 0;
            foreach (var value in embedding.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number
                    || !value.TryGetDouble(out var number)
                    || !double.IsFinite(number))
                {
                    throw new EmbeddingVectorValidationException(
                        "The embedding provider returned a non-finite vector value.");
                }

                values[valueIndex++] = number;
            }

            var index = item.TryGetProperty("index", out var indexElement)
                && indexElement.TryGetInt32(out var suppliedIndex)
                ? suppliedIndex
                : fallbackIndex;
            fallbackIndex++;

            if (index < 0 || index >= expectedCount || !usedIndexes.Add(index))
            {
                throw new EmbeddingVectorValidationException(
                    "The embedding provider returned invalid vector indexes.");
            }

            indexedVectors[index] = (index, values);
        }

        var dimensions = indexedVectors[0].Values.Length;
        if (dimensions is < EmbeddingVectorCodec.MinDimensions or > EmbeddingVectorCodec.MaxDimensions)
        {
            throw new EmbeddingVectorValidationException(
                "The embedding provider returned unsupported vector dimensions.");
        }

        if (indexedVectors.Any(vector => vector.Values.Length != dimensions))
        {
            throw new EmbeddingVectorValidationException(
                "The embedding provider returned inconsistent vector dimensions.");
        }

        return indexedVectors
            .Select(vector => new EmbeddingVector(vector.Values))
            .ToArray();
    }

    private sealed record EmbeddingRequest(string Model, IReadOnlyList<string> Input);
}
