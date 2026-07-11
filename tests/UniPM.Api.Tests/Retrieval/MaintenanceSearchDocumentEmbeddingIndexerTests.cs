using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class MaintenanceSearchDocumentEmbeddingIndexerTests
{
    [Fact]
    public async Task Indexer_creates_skips_and_updates_embeddings_in_bounded_batches()
    {
        var factory = new InMemoryContextFactory();
        await AddDocumentsAsync(factory);
        var service = new DeterministicEmbeddingService(
            input => input.Contains("alpha", StringComparison.Ordinal)
                ? [1d, 0d]
                : [0d, 1d]);
        await AddEmbeddingAsync(factory, GetInspectionId("doc-2"), service.Descriptor, "stale", "[1,0]");

        var indexer = CreateIndexer(factory, service, maxBatchSize: 2);
        var first = await indexer.RebuildAsync();
        var second = await indexer.RebuildAsync();

        Assert.Equal(new MaintenanceEmbeddingIndexResult(3, 2, 1, 0, 0), first);
        Assert.Equal(new MaintenanceEmbeddingIndexResult(3, 0, 0, 3, 0), second);
        Assert.Equal(2, service.Batches.Count);

        await using var context = factory.CreateDbContext();
        Assert.Equal(3, await context.MaintenanceSearchDocumentEmbeddings.CountAsync());
    }

    [Fact]
    public async Task Provider_failure_reports_failed_batch_without_changing_source_documents()
    {
        var factory = new InMemoryContextFactory();
        await AddDocumentsAsync(factory);
        var service = new DeterministicEmbeddingService(_ => [1d, 0d])
        {
            FailExecution = true
        };
        var indexer = CreateIndexer(factory, service, maxBatchSize: 2);

        var result = await indexer.RebuildAsync();

        Assert.Equal(new MaintenanceEmbeddingIndexResult(3, 0, 0, 0, 3), result);
        await using var context = factory.CreateDbContext();
        Assert.Equal(3, await context.MaintenanceSearchDocuments.CountAsync());
        Assert.Equal(0, await context.MaintenanceSearchDocumentEmbeddings.CountAsync());
    }

    [Fact]
    public async Task Disabled_indexing_returns_typed_availability_failure()
    {
        var factory = new InMemoryContextFactory();
        var service = new DisabledEmbeddingService();
        var indexer = CreateIndexer(factory, service, maxBatchSize: 2);

        await Assert.ThrowsAsync<EmbeddingServiceAvailabilityException>(
            () => indexer.RebuildAsync());
    }

    private static MaintenanceSearchDocumentEmbeddingIndexer CreateIndexer(
        InMemoryContextFactory factory,
        IEmbeddingService service,
        int maxBatchSize)
    {
        return new MaintenanceSearchDocumentEmbeddingIndexer(
            factory,
            service,
            Options.Create(new EmbeddingOptions
            {
                Enabled = true,
                MaxBatchSize = maxBatchSize,
                MaxInputCharacters = 4000
            }));
    }

    private static async Task AddDocumentsAsync(InMemoryContextFactory factory)
    {
        await using var context = factory.CreateDbContext();
        context.MaintenanceSearchDocuments.AddRange(
            CreateDocument("doc-1", "alpha"),
            CreateDocument("doc-2", "beta"),
            CreateDocument("doc-3", "gamma"));
        await context.SaveChangesAsync();
    }

    private static async Task AddEmbeddingAsync(
        InMemoryContextFactory factory,
        Guid inspectionId,
        EmbeddingServiceDescriptor descriptor,
        string sourceHash,
        string vectorJson)
    {
        await using var context = factory.CreateDbContext();
        context.MaintenanceSearchDocumentEmbeddings.Add(new MaintenanceSearchDocumentEmbedding
        {
            InspectionId = inspectionId,
            ProviderKey = descriptor.ProviderKey,
            ModelKey = descriptor.ModelKey,
            EmbeddingProfile = descriptor.EmbeddingProfile,
            Dimensions = 2,
            VectorJson = vectorJson,
            SourceHash = sourceHash,
            GeneratedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static MaintenanceSearchDocument CreateDocument(string suffix, string text)
    {
        return new MaintenanceSearchDocument
        {
            InspectionId = GetInspectionId(suffix),
            AssetId = Guid.NewGuid(),
            ScheduleId = Guid.NewGuid(),
            AssetCode = $"ASSET-{suffix}",
            AssetCategory = "fire-extinguisher",
            DateInspected = DateTimeOffset.UtcNow,
            SourceCreatedAt = DateTimeOffset.UtcNow,
            SourceUpdatedAt = DateTimeOffset.UtcNow,
            AssetUpdatedAt = DateTimeOffset.UtcNow,
            ProjectionVersion = "1.0.0",
            LexiconVersion = "1.0.0",
            IssueKeysJson = "[]",
            SearchText = text
        };
    }

    private static Guid GetInspectionId(string suffix)
        => Guid.Parse($"00000000-0000-0000-0000-00000000000{suffix[^1]}");

    private sealed class InMemoryContextFactory
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-embedding-indexer-{Guid.NewGuid():N}")
            .Options;

        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class DisabledEmbeddingService : IEmbeddingService
    {
        public EmbeddingServiceDescriptor Descriptor { get; } = new(
            false,
            "openai-compatible",
            string.Empty,
            null,
            "openai-compatible::maintenance-search-document-embedding-v1:unknown");

        public Task<IReadOnlyList<EmbeddingVector>> GenerateBatchAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
            => throw new EmbeddingServiceAvailabilityException("disabled");
    }
}
