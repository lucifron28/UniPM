using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class SqlServerLexicalMaintenanceRetrieverProviderTests
{
    [Fact]
    public async Task Non_sql_server_provider_is_rejected_without_a_fallback()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-lexical-provider-{Guid.NewGuid():N}")
            .Options;
        var retriever = new SqlServerLexicalMaintenanceRetriever(new InMemoryContextFactory(options));

        var exception = await Assert.ThrowsAsync<LexicalMaintenanceAvailabilityException>(
            () => retriever.SearchAsync(new LexicalMaintenanceSearchRequest("pressure")));

        Assert.Contains("SQL Server EF Core provider", exception.Message, StringComparison.Ordinal);
    }

    private sealed class InMemoryContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
