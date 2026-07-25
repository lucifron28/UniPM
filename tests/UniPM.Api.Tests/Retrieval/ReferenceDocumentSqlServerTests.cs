using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class ReferenceDocumentSqlServerTests
{
    [SqlServer2019Fact]
    public async Task Fixture_seed_preserves_unrelated_synthetic_records_and_reference_sections_are_full_text_searchable()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var service = new ReferenceDocumentRegistrationService(factory);
        var seeder = new SyntheticReferenceDocumentSeeder(factory, service);
        var seeded = await seeder.SeedAsync();
        Assert.Equal(3, seeded.Documents);

        await using (var context = factory.CreateDbContext())
        {
            context.ReferenceDocuments.Add(NewDocument(
                Guid.NewGuid(),
                "OTHER-FIXTURE",
                "R1",
                "another-fixture",
                lifecycleStatus: "Archived"));
            await context.SaveChangesAsync();
        }

        Assert.True(await WaitForContainsAsync(database.ConnectionString, "pressure"));
        await seeder.ResetAsync();

        await using var verification = factory.CreateDbContext();
        var survivor = await verification.ReferenceDocuments.SingleAsync();
        Assert.True(survivor.IsSynthetic);
        Assert.Equal("another-fixture", survivor.SyntheticFixtureKey);
    }

    [SqlServer2019Fact]
    public async Task Registration_normalizes_applicability_and_persists_active_and_superseded_revisions()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var service = new ReferenceDocumentRegistrationService(factory);
        var activeId = Guid.NewGuid();
        var supersededId = Guid.NewGuid();
        await service.RegisterOrUpdateAsync(CreateRegistration(activeId, "FIC-SQL-001", "R2", "Active", null));
        await service.RegisterOrUpdateAsync(CreateRegistration(supersededId, "FIC-SQL-001", "R1", "Superseded", activeId));

        await using var verification = factory.CreateDbContext();
        var active = await verification.ReferenceDocuments.SingleAsync(document => document.Id == activeId);
        var superseded = await verification.ReferenceDocuments
            .Include(document => document.Applicabilities)
            .SingleAsync(document => document.Id == supersededId);
        Assert.Equal("Active", active.LifecycleStatus);
        Assert.Equal("Superseded", superseded.LifecycleStatus);
        Assert.Equal(activeId, superseded.SupersededByDocumentId);
        var applicability = Assert.Single(superseded.Applicabilities);
        Assert.Equal("fire-extinguisher", applicability.AssetCategory);
        Assert.Equal("Fictional Maker", applicability.Manufacturer);
    }

    [SqlServer2019Fact]
    public async Task Sql_constraints_reject_duplicate_identity_sequence_and_invalid_section_embeddings()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var service = new ReferenceDocumentRegistrationService(factory);
        var documentId = Guid.NewGuid();
        await service.RegisterOrUpdateAsync(CreateRegistration(documentId, "FIC-SQL-002", "R1", "Active", null));

        await using (var duplicateDocumentContext = factory.CreateDbContext())
        {
            duplicateDocumentContext.ReferenceDocuments.Add(NewDocument(Guid.NewGuid(), "FIC-SQL-002", "R1", "other"));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDocumentContext.SaveChangesAsync());
        }

        await using (var duplicateSectionContext = factory.CreateDbContext())
        {
            duplicateSectionContext.ReferenceDocumentSections.Add(NewSection(documentId, 0));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateSectionContext.SaveChangesAsync());
        }

        var sectionId = await factory.CreateDbContext().ReferenceDocumentSections
            .Select(section => section.Id)
            .SingleAsync();
        await AssertInvalidEmbeddingAsync(factory, sectionId, 2, "not-json");
        await AssertInvalidEmbeddingAsync(factory, sectionId, 0, "[1,0]");
    }

    [SqlServer2019Fact]
    public async Task Section_deletion_cascades_its_embedding()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var service = new ReferenceDocumentRegistrationService(factory);
        await service.RegisterOrUpdateAsync(CreateRegistration(Guid.NewGuid(), "FIC-SQL-003", "R1", "Active", null));
        await using (var context = factory.CreateDbContext())
        {
            var section = await context.ReferenceDocumentSections.SingleAsync();
            context.ReferenceDocumentSectionEmbeddings.Add(NewEmbedding(section.Id, section.SectionHash, 2, "[1,0]"));
            await context.SaveChangesAsync();
            context.ReferenceDocumentSections.Remove(section);
            await context.SaveChangesAsync();
        }

        await using var verification = factory.CreateDbContext();
        Assert.Empty(await verification.ReferenceDocumentSectionEmbeddings.ToListAsync());
    }

    [SqlServer2019Fact]
    public async Task Sql_constraints_reject_invalid_supersession_lifecycle_combinations()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
            context.ReferenceDocuments.Add(NewDocument(Guid.NewGuid(), "FIC-SQL-LINK", "R2", "target"));
            await context.SaveChangesAsync();
        }

        Guid targetId;
        await using (var context = factory.CreateDbContext())
        {
            targetId = await context.ReferenceDocuments.Select(document => document.Id).SingleAsync();
        }

        await AssertInvalidSupersessionAsync(factory, WithSupersession(
            NewDocument(Guid.NewGuid(), "FIC-SQL-LINK", "R1", "active-link"), targetId));
        await AssertInvalidSupersessionAsync(factory, WithSupersession(
            NewDocument(Guid.NewGuid(), "FIC-SQL-LINK", "R3", "archived-link", "Archived"), targetId));
        await AssertInvalidSupersessionAsync(factory, NewDocument(Guid.NewGuid(), "FIC-SQL-LINK", "R4", "missing-link", "Superseded"));

        var selfId = Guid.NewGuid();
        await AssertInvalidSupersessionAsync(factory, WithSupersession(
            NewDocument(selfId, "FIC-SQL-LINK", "R5", "self-link", "Superseded"), selfId));
    }

    private static async Task AssertInvalidEmbeddingAsync(
        TestContextFactory factory,
        Guid sectionId,
        int dimensions,
        string vectorJson)
    {
        await using var context = factory.CreateDbContext();
        var section = await context.ReferenceDocumentSections.SingleAsync();
        context.ReferenceDocumentSectionEmbeddings.Add(NewEmbedding(sectionId, section.SectionHash, dimensions, vectorJson));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task AssertInvalidSupersessionAsync(
        TestContextFactory factory,
        ReferenceDocument document)
    {
        await using var context = factory.CreateDbContext();
        context.ReferenceDocuments.Add(document);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static ReferenceDocumentRegistration CreateRegistration(
        Guid id,
        string sourceKey,
        string revision,
        string lifecycleStatus,
        Guid? supersededByDocumentId)
        => new(
            id,
            "Institutional",
            sourceKey,
            "Fictional SQL reference",
            "Fictional Authority",
            revision,
            lifecycleStatus,
            new DateOnly(2026, 1, 1),
            supersededByDocumentId,
            true,
            "sql-reference-test",
            [new ReferenceDocumentApplicabilityRegistration(" FIRE-EXTINGUISHER ", " Fictional Maker ", null, null, " Test scope ")],
            [new ReferenceDocumentSectionRegistration(
                Guid.NewGuid(),
                0,
                "Fictional pressure section",
                $"{sourceKey} {revision}, page 1",
                1,
                1,
                "Fictional pressure observation for Full-Text Search.")]);

    private static ReferenceDocument NewDocument(
        Guid id,
        string sourceKey,
        string revision,
        string fixtureKey,
        string lifecycleStatus = "Active")
        => new()
        {
            Id = id,
            SourceType = "Institutional",
            SourceKey = sourceKey,
            Title = "Fictional direct SQL document",
            PublisherAuthority = "Fictional Authority",
            Revision = revision,
            LifecycleStatus = lifecycleStatus,
            ImportedAt = DateTimeOffset.UtcNow,
            ContentChecksum = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            IsSynthetic = true,
            SyntheticFixtureKey = fixtureKey
        };

    private static ReferenceDocumentSection NewSection(Guid documentId, int sequence)
        => new()
        {
            Id = Guid.NewGuid(),
            ReferenceDocumentId = documentId,
            Sequence = sequence,
            Heading = "Fictional duplicate section",
            SourceLocator = "FIC-SQL, page 1",
            SectionText = "Fictional SQL constraint test.",
            SectionHash = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static ReferenceDocument WithSupersession(ReferenceDocument document, Guid supersededByDocumentId)
    {
        document.SupersededByDocumentId = supersededByDocumentId;
        return document;
    }

    private static ReferenceDocumentSectionEmbedding NewEmbedding(
        Guid sectionId,
        string sectionHash,
        int dimensions,
        string vectorJson)
        => new()
        {
            ReferenceDocumentSectionId = sectionId,
            ProviderKey = "test-provider",
            ModelKey = "test-model",
            EmbeddingProfile = "test-provider:test-model:reference-section-v1:2",
            Dimensions = dimensions,
            VectorJson = vectorJson,
            SectionHash = sectionHash,
            GeneratedAt = DateTimeOffset.UtcNow
        };

    private static async Task<bool> WaitForContainsAsync(string connectionString, string term)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(timeout.Token);
                await using var command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandText = "SELECT COUNT(*) FROM CONTAINSTABLE(dbo.ReferenceDocumentSections, (Heading, SectionText), @term);";
                command.Parameters.AddWithValue("@term", $"\"{term}\"");
                if (Convert.ToInt32(await command.ExecuteScalarAsync(timeout.Token)) > 0)
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return false;
        }
    }

    private static string RequireSqlServer2019Connection()
        => Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION")!;

    private sealed class TestContextFactory(string connectionString) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
            => new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(connectionString)
                .Options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private readonly string databaseName;

        private SqlServerTestDatabase(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            this.databaseName = databaseName;
        }

        public string ConnectionString { get; }

        public static async Task<SqlServerTestDatabase> CreateAsync(string baseConnectionString)
        {
            var databaseName = $"UniPMReferenceTests_{Guid.NewGuid():N}";
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(masterBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
            return new SqlServerTestDatabase(databaseBuilder.ConnectionString, databaseName);
        }

        public async ValueTask DisposeAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
