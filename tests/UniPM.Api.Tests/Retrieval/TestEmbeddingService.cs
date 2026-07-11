using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

internal sealed class DeterministicEmbeddingService(
    Func<string, IReadOnlyList<double>> vectorFactory,
    int dimensions = 2,
    string providerKey = "test-provider",
    string modelKey = "test-model",
    string? profile = null)
    : IEmbeddingService
{
    public EmbeddingServiceDescriptor Descriptor { get; } = new(
        true,
        providerKey,
        modelKey,
        dimensions,
        profile ?? $"{providerKey}:{modelKey}:maintenance-search-document-embedding-v1:{dimensions}");

    public List<IReadOnlyList<string>> Batches { get; } = [];
    public bool FailExecution { get; set; }

    public Task<IReadOnlyList<EmbeddingVector>> GenerateBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        Batches.Add(inputs);
        if (FailExecution)
        {
            throw new EmbeddingServiceExecutionException("Deterministic provider failure.");
        }

        return Task.FromResult<IReadOnlyList<EmbeddingVector>>(
            inputs.Select(input => new EmbeddingVector(vectorFactory(input))).ToArray());
    }
}
