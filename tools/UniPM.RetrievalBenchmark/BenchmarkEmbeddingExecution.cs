using System.Collections.Concurrent;
using System.Diagnostics;
using UniPM.Api.Features.Retrieval;

namespace UniPM.RetrievalBenchmark;

internal sealed record BenchmarkEmbeddingExecutionPlan(
    string ProviderKey,
    string ModelKey,
    int? Dimensions,
    int DocumentCount,
    int BatchSize,
    int QueryEmbeddingCount)
{
    public int DocumentBatchCount => (DocumentCount + BatchSize - 1) / BatchSize;
    public int ExpectedProviderRequestCount => DocumentBatchCount + QueryEmbeddingCount;

    public string ToSafeSummary()
        => $"Approved embedding execution plan: provider={ProviderKey}; model={ModelKey}; dimensions={Dimensions}; documents={DocumentCount}; batchSize={BatchSize}; documentBatches={DocumentBatchCount}; queryEmbeddings={QueryEmbeddingCount}; expectedProviderRequests={ExpectedProviderRequestCount}.";
}

internal sealed class BenchmarkEmbeddingExecutionTracker(BenchmarkEmbeddingExecutionPlan plan)
{
    private readonly object gate = new();
    private int providerRequestCount;
    private int embeddedInputCount;
    private int queryCacheHitCount;
    private double providerDurationMilliseconds;

    public void RecordProviderRequest(int inputCount, TimeSpan duration)
    {
        lock (gate)
        {
            providerRequestCount++;
            embeddedInputCount += inputCount;
            providerDurationMilliseconds += duration.TotalMilliseconds;
        }
    }

    public void RecordQueryCacheHit()
    {
        lock (gate)
        {
            queryCacheHitCount++;
        }
    }

    public BenchmarkEmbeddingExecutionReport CreateReport()
    {
        lock (gate)
        {
            return new BenchmarkEmbeddingExecutionReport
            {
                ProviderKey = plan.ProviderKey,
                ModelKey = plan.ModelKey,
                Dimensions = plan.Dimensions,
                DocumentCount = plan.DocumentCount,
                BatchSize = plan.BatchSize,
                DocumentBatchCount = plan.DocumentBatchCount,
                QueryEmbeddingCount = plan.QueryEmbeddingCount,
                ExpectedProviderRequestCount = plan.ExpectedProviderRequestCount,
                ActualProviderRequestCount = providerRequestCount,
                ActualEmbeddedInputCount = embeddedInputCount,
                QueryCacheHitCount = queryCacheHitCount,
                ProviderDurationMilliseconds = providerDurationMilliseconds
            };
        }
    }
}

internal sealed class BenchmarkCachingEmbeddingService(
    IEmbeddingService inner,
    BenchmarkEmbeddingExecutionTracker tracker)
    : IEmbeddingService
{
    private readonly ConcurrentDictionary<string, Lazy<Task<EmbeddingVector>>> queryCache = new(StringComparer.Ordinal);

    public EmbeddingServiceDescriptor Descriptor => inner.Descriptor;

    public async Task<IReadOnlyList<EmbeddingVector>> GenerateBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs.Count != 1)
        {
            return await GenerateAndTrackAsync(inputs, cancellationToken);
        }

        var candidate = new Lazy<Task<EmbeddingVector>>(
            () => GenerateSingleAndTrackAsync(inputs[0], cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var cached = queryCache.GetOrAdd(inputs[0], candidate);
        if (!ReferenceEquals(candidate, cached))
        {
            tracker.RecordQueryCacheHit();
        }

        return [await cached.Value];
    }

    private async Task<EmbeddingVector> GenerateSingleAndTrackAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var vectors = await GenerateAndTrackAsync([input], cancellationToken);
        if (vectors.Count != 1)
        {
            throw new EmbeddingVectorValidationException(
                "The embedding provider returned a vector count different from the requested query batch.");
        }

        return vectors[0];
    }

    private async Task<IReadOnlyList<EmbeddingVector>> GenerateAndTrackAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await inner.GenerateBatchAsync(inputs, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
            tracker.RecordProviderRequest(inputs.Count, stopwatch.Elapsed);
        }
    }
}
