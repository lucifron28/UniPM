using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class SemanticMaintenanceRetrieverProviderTests
{
    [Fact]
    public async Task Disabled_provider_returns_semantic_availability_failure()
    {
        var retriever = CreateRetriever(new DisabledEmbeddingService());

        await Assert.ThrowsAsync<SemanticMaintenanceAvailabilityException>(
            () => retriever.SearchAsync(new SemanticMaintenanceSearchRequest("pressure")));
    }

    [Fact]
    public async Task Non_sql_server_provider_is_rejected_before_embedding_generation()
    {
        var service = new DeterministicEmbeddingService(_ => [1d, 0d])
        {
            FailExecution = true
        };
        var retriever = CreateRetriever(service);

        await Assert.ThrowsAsync<SemanticMaintenanceAvailabilityException>(
            () => retriever.SearchAsync(new SemanticMaintenanceSearchRequest("pressure")));
        Assert.Empty(service.Batches);
    }

    private static SqlServerSemanticMaintenanceRetriever CreateRetriever(IEmbeddingService service)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-semantic-provider-{Guid.NewGuid():N}")
            .Options;
        return new SqlServerSemanticMaintenanceRetriever(
            new InMemoryContextFactory(options),
            service,
            CreateIssueNormalizer());
    }

    private static MaintenanceIssueNormalizer CreateIssueNormalizer()
    {
        var root = FindRepositoryRoot();
        var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = Path.Combine(
                root,
                "server",
                "Features",
                "Retrieval",
                "Resources",
                MaintenanceIssueLexiconOptions.LexiconFileName)
        });
        return new MaintenanceIssueNormalizer(loader);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root.");
    }

    private sealed class InMemoryContextFactory(
        DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
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
