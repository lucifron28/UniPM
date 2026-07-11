using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

internal sealed class DeterministicEmbeddingService(
    Func<string, IReadOnlyList<double>> vectorFactory,
    int dimensions = 2)
    : IEmbeddingService
{
    public EmbeddingServiceDescriptor Descriptor { get; } = new(
        true,
        "test-provider",
        "test-model",
        dimensions,
        "test-provider:test-model:maintenance-search-document-embedding-v1:2");

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
