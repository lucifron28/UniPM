using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.ReferenceDocuments;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class ReferenceDocumentSqlServerTests
{
    [SqlServer2019Fact]
    public async Task Institutional_embedding_rebuild_indexes_future_sections_and_refreshes_stale_vectors()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var seeder = new SyntheticReferenceDocumentSeeder(factory, new ReferenceDocumentRegistrationService(factory));
        await seeder.SeedAsync();
        var embeddingService = new DeterministicEmbeddingService(_ => [1d, 0d]);
        var indexer = new InstitutionalReferenceEmbeddingIndexer(
            factory,
            embeddingService,
            Options.Create(new EmbeddingOptions { MaxBatchSize = 4 }));
        var initial = await indexer.RebuildAsync();
        Assert.Equal(5, initial.Total);
        Assert.Equal(5, initial.Created);
        Assert.Equal(0, initial.Updated);
        Assert.Equal(0, initial.Skipped);
        Assert.Equal(0, initial.Failed);

        var current = await indexer.RebuildAsync();
        Assert.Equal(5, current.Total);
        Assert.Equal(0, current.Created);
        Assert.Equal(0, current.Updated);
        Assert.Equal(5, current.Skipped);
        Assert.Equal(0, current.Failed);

        await using (var context = factory.CreateDbContext())
        {
            var stale = await context.ReferenceDocumentSectionEmbeddings
                .OrderBy(embedding => embedding.ReferenceDocumentSectionId)
                .FirstAsync();
            stale.EmbeddingProfile = "stale-profile";
            stale.Dimensions = 3;
            await context.SaveChangesAsync();
        }

        var refreshed = await indexer.RebuildAsync();
        Assert.Equal(5, refreshed.Total);
        Assert.Equal(0, refreshed.Created);
        Assert.Equal(1, refreshed.Updated);
        Assert.Equal(4, refreshed.Skipped);
        Assert.Equal(0, refreshed.Failed);

        var retriever = new SqlServerSemanticInstitutionalReferenceRetriever(factory, embeddingService);
        var results = await retriever.SearchAsync(new InstitutionalReferenceSearchRequest(
            "panel",
            "fire-alarm",
            new DateOnly(2026, 1, 1)));

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.Equal("InstitutionalReference", result.EvidenceSourceGroup));
        Assert.DoesNotContain(results, result => result.SourceKey == "FIC-ALM-FUT");
        Assert.Equal(5, embeddingService.Batches.Count);
        await using var verification = factory.CreateDbContext();
        Assert.Equal(5, await verification.ReferenceDocumentSectionEmbeddings.CountAsync());
        Assert.NotNull(await verification.ReferenceDocumentSectionEmbeddings
            .Include(embedding => embedding.ReferenceDocumentSection)
            .ThenInclude(section => section!.ReferenceDocument)
            .SingleOrDefaultAsync(embedding => embedding.ReferenceDocumentSection!.ReferenceDocument!.SourceKey == "FIC-ALM-FUT"));
    }

    [SqlServer2019Fact]
    public Task Institutional_semantic_candidate_cap_is_false_for_499_eligible_candidates()
        => AssertInstitutionalCandidateCapAsync(499, false);

    [SqlServer2019Fact]
    public Task Institutional_semantic_candidate_cap_is_false_for_exactly_500_eligible_candidates()
        => AssertInstitutionalCandidateCapAsync(500, false);

    [SqlServer2019Fact]
    public Task Institutional_semantic_candidate_cap_is_true_for_501_eligible_candidates()
        => AssertInstitutionalCandidateCapAsync(501, true);

    private static async Task AssertInstitutionalCandidateCapAsync(
        int sectionCount,
        bool expectedCapReached)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var embeddingService = new DeterministicEmbeddingService(_ => [1d, 0d]);
        await SeedSemanticCandidatesAsync(factory, sectionCount, embeddingService.Descriptor);
        var diagnostics = new InstitutionalReferenceRetrievalDiagnostics();
        var retriever = new SqlServerSemanticInstitutionalReferenceRetriever(factory, embeddingService, diagnostics);

        var results = await retriever.SearchAsync(new InstitutionalReferenceSearchRequest(
            "panel",
            "fire-alarm",
            new DateOnly(2026, 1, 1)));

        Assert.NotEmpty(results);
        var recorded = diagnostics.Consume();
        Assert.Equal(Math.Min(sectionCount, SqlServerSemanticInstitutionalReferenceRetriever.MaxCandidateCount),
            recorded.CandidateCount);
        Assert.Equal(expectedCapReached, recorded.CandidateCapReached);
        Assert.Equal(0, recorded.InvalidVectorCount);
    }

    [SqlServer2019Fact]
    public async Task Institutional_lexical_retrieval_returns_only_active_applicable_source_locatable_sections()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var factory = new TestContextFactory(database.ConnectionString);
        await using (var context = factory.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        var seeder = new SyntheticReferenceDocumentSeeder(factory, new ReferenceDocumentRegistrationService(factory));
        await seeder.SeedAsync();
        Assert.True(await WaitForContainsAsync(database.ConnectionString, "panel"));

        var retriever = new SqlServerLexicalInstitutionalReferenceRetriever(factory);
        var results = await retriever.SearchAsync(new InstitutionalReferenceSearchRequest(
            "panel",
            "fire-alarm",
            new DateOnly(2026, 1, 1)));

        var result = Assert.Single(results);
        Assert.Equal("InstitutionalReference", result.EvidenceSourceGroup);
        Assert.Equal("FIC-SAF-001", result.SourceKey);
        Assert.Equal("R2", result.Revision);
        Assert.NotEmpty(result.SourceLocator);
        Assert.NotNull(result.PageStart);
        Assert.DoesNotContain(results, item => item.SourceKey == "FIC-ALM-FUT");

        var categoryWide = await retriever.SearchAsync(new InstitutionalReferenceSearchRequest(
            "authorized personnel",
            "water-drinking-station",
            new DateOnly(2026, 1, 1)));
        Assert.Contains(categoryWide, item =>
            item.ApplicabilityMatch == InstitutionalReferenceApplicabilityMatch.CategoryWide
            && !string.IsNullOrWhiteSpace(item.MatchedScopeLabel));
        Assert.Equal("Category-wide fictional observation recording", categoryWide[0].MatchedScopeLabel);
    }

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
        Assert.Equal(7, seeded.Documents);

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

    private static async Task SeedSemanticCandidatesAsync(
        TestContextFactory factory,
        int sectionCount,
        EmbeddingServiceDescriptor descriptor)
    {
        var document = new ReferenceDocument
        {
            Id = Guid.NewGuid(),
            SourceType = ReferenceDocumentSourceTypeCatalog.Institutional,
            SourceKey = "FIC-CAP-001",
            Title = "Fictional semantic candidate cap document",
            PublisherAuthority = "Fictional University General Services Office",
            Revision = "R1",
            LifecycleStatus = ReferenceDocumentLifecycleCatalog.Active,
            EffectiveDate = new DateOnly(2025, 1, 1),
            ImportedAt = DateTimeOffset.UtcNow,
            ContentChecksum = "FICCAP001",
            IsSynthetic = true,
            SyntheticFixtureKey = "semantic-candidate-cap"
        };
        document.Applicabilities.Add(new ReferenceDocumentApplicability
        {
            Id = Guid.NewGuid(),
            ReferenceDocumentId = document.Id,
            AssetCategory = "fire-alarm",
            ScopeLabel = "Fictional fire alarm cap scope"
        });
        for (var index = 0; index < sectionCount; index++)
        {
            document.Sections.Add(new ReferenceDocumentSection
            {
                Id = Guid.NewGuid(),
                ReferenceDocumentId = document.Id,
                Sequence = index,
                Heading = $"Fictional panel observation {index}",
                SourceLocator = $"FIC-CAP-001 R1, section {index + 1}",
                SectionText = "Fictional alarm panel observation for semantic candidate-cap verification.",
                SectionHash = $"FICCAP{index:D4}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await using var context = factory.CreateDbContext();
        context.ReferenceDocuments.Add(document);
        await context.SaveChangesAsync();

        var profile = InstitutionalReferenceEmbeddingInput.BuildProfile(descriptor);
        foreach (var section in document.Sections)
        {
            context.ReferenceDocumentSectionEmbeddings.Add(new ReferenceDocumentSectionEmbedding
            {
                ReferenceDocumentSectionId = section.Id,
                ProviderKey = descriptor.ProviderKey,
                ModelKey = descriptor.ModelKey,
                EmbeddingProfile = profile,
                Dimensions = descriptor.Dimensions!.Value,
                VectorJson = "[1,0]",
                SectionHash = InstitutionalReferenceEmbeddingInput.ComputeSectionSourceHash(section, document),
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();
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
