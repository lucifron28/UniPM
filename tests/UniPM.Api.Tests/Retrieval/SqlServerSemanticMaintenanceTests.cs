using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class SqlServerSemanticMaintenanceTests
{
    private const string PreviousMigration = "20260711120000_AddMaintenanceFullTextSearch";
    private const string EmbeddingMigration = "20260711123548_AddMaintenanceSearchDocumentEmbeddings";

    [SqlServerFact]
    public async Task Embedding_migration_applies_rolls_back_reapplies_and_enforces_constraints()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            Assert.Contains(
                EmbeddingMigration,
                await context.Database.GetAppliedMigrationsAsync());
        }

        Assert.True(await database.HasEmbeddingTableAsync());

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
        }

        Assert.False(await database.HasEmbeddingTableAsync());

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.True(await database.HasEmbeddingTableAsync());

        var document = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 5, 1),
            "pressure",
            [1d, 0d]);

        var invalidDimensionsDocument = await AddDocumentAsync(
            database,
            "fire-alarm",
            "Annex",
            "Security",
            "Control Room",
            true,
            AtManila(2025, 5, 2),
            "invalid dimensions",
            null);

        await using (var context = database.CreateContext())
        {
            context.MaintenanceSearchDocumentEmbeddings.Add(new MaintenanceSearchDocumentEmbedding
            {
                InspectionId = invalidDimensionsDocument.InspectionId,
                ProviderKey = "test-provider",
                ModelKey = "test-model",
                EmbeddingProfile = TestProfile,
                Dimensions = 0,
                VectorJson = "[1,0]",
                SourceHash = MaintenanceEmbeddingInput.ComputeSourceHash(document.SearchText),
                GeneratedAt = DateTimeOffset.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        var invalidJsonDocument = await AddDocumentAsync(
            database,
            "fire-alarm",
            "Annex",
            "Security",
            "Control Room",
            true,
            AtManila(2025, 5, 3),
            "invalid json",
            null);

        await using (var context = database.CreateContext())
        {
            context.MaintenanceSearchDocumentEmbeddings.Add(new MaintenanceSearchDocumentEmbedding
            {
                InspectionId = invalidJsonDocument.InspectionId,
                ProviderKey = "test-provider",
                ModelKey = "test-model",
                EmbeddingProfile = TestProfile,
                Dimensions = 2,
                VectorJson = "not-json",
                SourceHash = MaintenanceEmbeddingInput.ComputeSourceHash(document.SearchText),
                GeneratedAt = DateTimeOffset.UtcNow
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using (var context = database.CreateContext())
        {
            var storedDocument = await context.MaintenanceSearchDocuments
                .SingleAsync(item => item.InspectionId == document.InspectionId);
            context.MaintenanceSearchDocuments.Remove(storedDocument);
            await context.SaveChangesAsync();
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(0, await verificationContext.MaintenanceSearchDocumentEmbeddings.CountAsync());
    }

    [SqlServerFact]
    public async Task Semantic_retrieval_filters_stale_data_and_orders_source_traceable_results()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var pressure = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 5, 1),
            "low pressure",
            [1d, 0d]);
        var pressureTie = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 5, 1),
            "pressure gauge",
            [1d, 0d],
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var alarm = await AddDocumentAsync(
            database,
            "fire-alarm",
            "Annex",
            "Security",
            "Control Room",
            true,
            AtManila(2025, 6, 1),
            "alarm panel fault",
            [0d, 1d]);
        var stale = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 4, 1),
            "stale pressure",
            [1d, 0d],
            embeddingSourceHash: "stale-source-hash");
        var wrongProfile = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 3, 1),
            "wrong profile pressure",
            [1d, 0d],
            embeddingProfile: "other-provider:other-model:maintenance-search-document-embedding-v1:2");
        var malformed = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 2, 1),
            "malformed pressure",
            null,
            malformedVectorJson: "{}");

        var service = new DeterministicEmbeddingService(
            input => input.Contains("alarm", StringComparison.OrdinalIgnoreCase)
                ? [0d, 1d]
                : [1d, 0d]);
        var retriever = new SqlServerSemanticMaintenanceRetriever(
            new TestContextFactory(database.ConnectionString),
            service,
            CreateIssueNormalizer());

        var results = await retriever.SearchAsync(
            new SemanticMaintenanceSearchRequest(
                "pressure",
                AssetCategory: " FIRE-EXTINGUISHER ",
                Building: " Main Building ",
                Department: " Administration ",
                Location: " Ground Floor ",
                IsOperational: false,
                DateFrom: AtManila(2025, 5, 1),
                DateTo: AtManila(2025, 5, 1, 23, 59)));

        Assert.Equal(2, results.Count);
        Assert.Equal(pressureTie.InspectionId, results[0].InspectionId);
        Assert.Equal(pressure.InspectionId, results[1].InspectionId);
        Assert.DoesNotContain(results, result => result.InspectionId == stale.InspectionId);
        Assert.DoesNotContain(results, result => result.InspectionId == wrongProfile.InspectionId);
        Assert.DoesNotContain(results, result => result.InspectionId == malformed.InspectionId);
        Assert.All(results, result => Assert.Equal("semantic", result.RetrievalChannel));
        Assert.Null(typeof(SemanticMaintenanceSearchResult).GetProperty("SearchText"));
        Assert.Null(typeof(SemanticMaintenanceSearchResult).GetProperty("VectorJson"));
        Assert.Null(typeof(SemanticMaintenanceSearchResult).GetProperty("IssueKeysJson"));

        var filtered = await retriever.SearchAsync(
            new SemanticMaintenanceSearchRequest(
                "alarm",
                AssetCategory: "fire-alarm",
                IsOperational: true));
        var alarmResult = Assert.Single(filtered);
        Assert.Equal(alarm.InspectionId, alarmResult.InspectionId);

        var empty = await retriever.SearchAsync(
            new SemanticMaintenanceSearchRequest(
                "pressure",
                AssetId: Guid.NewGuid()));
        Assert.Empty(empty);
    }

    [SqlServerFact]
    public async Task Embedding_indexer_persists_skips_and_regenerates_with_a_fake_provider()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var document = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 10, 1),
            "pressure",
            null);
        var service = new DeterministicEmbeddingService(_ => [1d, 0d]);
        var indexer = new MaintenanceSearchDocumentEmbeddingIndexer(
            new TestContextFactory(database.ConnectionString),
            service,
            Microsoft.Extensions.Options.Options.Create(new EmbeddingOptions
            {
                Enabled = true,
                ProviderKey = "test-provider",
                Model = "test-model",
                Dimensions = 2,
                MaxBatchSize = 2,
                MaxInputCharacters = 4000
            }));

        var created = await indexer.RebuildAsync();
        var skipped = await indexer.RebuildAsync();

        await using (var context = database.CreateContext())
        {
            var stored = await context.MaintenanceSearchDocuments
                .SingleAsync(item => item.InspectionId == document.InspectionId);
            stored.SearchText = "refreshed pressure";
            await context.SaveChangesAsync();
        }

        var updated = await indexer.RebuildAsync();

        Assert.Equal(new MaintenanceEmbeddingIndexResult(1, 1, 0, 0, 0), created);
        Assert.Equal(new MaintenanceEmbeddingIndexResult(1, 0, 0, 1, 0), skipped);
        Assert.Equal(new MaintenanceEmbeddingIndexResult(1, 0, 1, 0, 0), updated);
        await using var verificationContext = database.CreateContext();
        var embedding = await verificationContext.MaintenanceSearchDocumentEmbeddings
            .SingleAsync(item => item.InspectionId == document.InspectionId);
        Assert.Equal(
            MaintenanceEmbeddingInput.ComputeSourceHash("refreshed pressure"),
            embedding.SourceHash);
    }

    [SqlServerFact]
    public async Task Older_valid_embeddings_are_not_hidden_by_newer_ineligible_documents()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var older = await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2020, 1, 1),
            "pressure",
            [1d, 0d]);
        await AddDistractorDocumentsAsync(database, 501, addWrongProfile: true);

        var service = new DeterministicEmbeddingService(_ => [1d, 0d]);
        var retriever = new SqlServerSemanticMaintenanceRetriever(
            new TestContextFactory(database.ConnectionString),
            service,
            CreateIssueNormalizer());

        var results = await retriever.SearchAsync(new SemanticMaintenanceSearchRequest("pressure"));

        var result = Assert.Single(results);
        Assert.Equal(older.InspectionId, result.InspectionId);
    }

    [SqlServerFact]
    public async Task No_eligible_candidates_do_not_generate_a_query_embedding()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        await AddDocumentAsync(
            database,
            "fire-extinguisher",
            "Main Building",
            "Administration",
            "Ground Floor",
            false,
            AtManila(2025, 1, 1),
            "unembedded",
            null);

        var service = new DeterministicEmbeddingService(_ => [1d, 0d]);
        var retriever = new SqlServerSemanticMaintenanceRetriever(
            new TestContextFactory(database.ConnectionString),
            service,
            CreateIssueNormalizer());

        var results = await retriever.SearchAsync(new SemanticMaintenanceSearchRequest("pressure"));

        Assert.Empty(results);
        Assert.Empty(service.Batches);
    }

    private const string TestProfile = "test-provider:test-model:maintenance-search-document-embedding-v1:2";

    private static async Task<SqlServerTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    private static async Task<SearchFixtureRecord> AddDocumentAsync(
        SqlServerTestDatabase database,
        string assetCategory,
        string building,
        string department,
        string location,
        bool isOperational,
        DateTimeOffset dateInspected,
        string searchText,
        IReadOnlyList<double>? vector,
        Guid? inspectionId = null,
        string? embeddingSourceHash = null,
        string? embeddingProfile = null,
        string? malformedVectorJson = null)
    {
        var record = new SearchFixtureRecord(
            inspectionId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"SEM-{Guid.NewGuid():N}"[..12],
            searchText);

        await using var context = database.CreateContext();
        context.Assets.Add(new Asset
        {
            Id = record.AssetId,
            AssetCode = record.AssetCode,
            AssetCategory = assetCategory,
            Building = building,
            Department = department,
            Location = location,
            Status = "Active",
            CreatedAt = dateInspected,
            UpdatedAt = dateInspected
        });
        context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
        {
            Id = record.ScheduleId,
            AssetId = record.AssetId,
            ScheduleDate = dateInspected,
            PeriodType = "Quarter",
            Status = "Completed",
            CreatedAt = dateInspected,
            UpdatedAt = dateInspected
        });
        context.InspectionRecords.Add(new InspectionRecord
        {
            Id = record.InspectionId,
            ScheduleId = record.ScheduleId,
            AssetId = record.AssetId,
            InspectorUserId = Guid.NewGuid(),
            DateInspected = dateInspected,
            IsOperational = isOperational,
            Remarks = searchText,
            CreatedAt = dateInspected,
            UpdatedAt = dateInspected
        });
        context.MaintenanceSearchDocuments.Add(new MaintenanceSearchDocument
        {
            InspectionId = record.InspectionId,
            AssetId = record.AssetId,
            ScheduleId = record.ScheduleId,
            AssetCode = record.AssetCode,
            AssetCategory = assetCategory,
            Building = building,
            Department = department,
            Location = location,
            DateInspected = dateInspected,
            IsOperational = isOperational,
            SourceCreatedAt = dateInspected,
            SourceUpdatedAt = dateInspected,
            AssetUpdatedAt = dateInspected,
            ProjectionVersion = "1.0.0",
            LexiconVersion = "1.0.0",
            IssueKeysJson = "[]",
            SearchText = searchText
        });

        if (vector is not null || malformedVectorJson is not null)
        {
            context.MaintenanceSearchDocumentEmbeddings.Add(new MaintenanceSearchDocumentEmbedding
            {
                InspectionId = record.InspectionId,
                ProviderKey = "test-provider",
                ModelKey = "test-model",
                EmbeddingProfile = embeddingProfile ?? TestProfile,
                Dimensions = 2,
                VectorJson = malformedVectorJson ?? EmbeddingVectorCodec.Serialize(vector!),
                SourceHash = embeddingSourceHash
                    ?? MaintenanceEmbeddingInput.ComputeSourceHash(searchText),
                GeneratedAt = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync();
        return record;
    }

    private static async Task AddDistractorDocumentsAsync(
        SqlServerTestDatabase database,
        int count,
        bool addWrongProfile)
    {
        await using var context = database.CreateContext();
        for (var index = 0; index < count; index++)
        {
            var inspectedAt = AtManila(2025, 1, 1).AddMinutes(index);
            var assetId = Guid.NewGuid();
            var scheduleId = Guid.NewGuid();
            var inspectionId = Guid.NewGuid();
            var assetCode = $"DIST-{Guid.NewGuid():N}"[..12];
            var searchText = $"distractor {index}";

            context.Assets.Add(new Asset
            {
                Id = assetId,
                AssetCode = assetCode,
                AssetCategory = "fire-extinguisher",
                Building = "Main Building",
                Department = "Administration",
                Location = "Ground Floor",
                Status = "Active",
                CreatedAt = inspectedAt,
                UpdatedAt = inspectedAt
            });
            context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = scheduleId,
                AssetId = assetId,
                ScheduleDate = inspectedAt,
                PeriodType = "Quarter",
                Status = "Completed",
                CreatedAt = inspectedAt,
                UpdatedAt = inspectedAt
            });
            context.InspectionRecords.Add(new InspectionRecord
            {
                Id = inspectionId,
                ScheduleId = scheduleId,
                AssetId = assetId,
                InspectorUserId = Guid.NewGuid(),
                DateInspected = inspectedAt,
                IsOperational = false,
                Remarks = searchText,
                CreatedAt = inspectedAt,
                UpdatedAt = inspectedAt
            });
            context.MaintenanceSearchDocuments.Add(new MaintenanceSearchDocument
            {
                InspectionId = inspectionId,
                AssetId = assetId,
                ScheduleId = scheduleId,
                AssetCode = assetCode,
                AssetCategory = "fire-extinguisher",
                Building = "Main Building",
                Department = "Administration",
                Location = "Ground Floor",
                DateInspected = inspectedAt,
                IsOperational = false,
                SourceCreatedAt = inspectedAt,
                SourceUpdatedAt = inspectedAt,
                AssetUpdatedAt = inspectedAt,
                ProjectionVersion = "1.0.0",
                LexiconVersion = "1.0.0",
                IssueKeysJson = "[]",
                SearchText = searchText
            });

            if (addWrongProfile && index % 2 == 0)
            {
                context.MaintenanceSearchDocumentEmbeddings.Add(new MaintenanceSearchDocumentEmbedding
                {
                    InspectionId = inspectionId,
                    ProviderKey = "other-provider",
                    ModelKey = "other-model",
                    EmbeddingProfile = "other-profile",
                    Dimensions = 2,
                    VectorJson = EmbeddingVectorCodec.Serialize([1d, 0d]),
                    SourceHash = MaintenanceEmbeddingInput.ComputeSourceHash(searchText),
                    GeneratedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static MaintenanceIssueNormalizer CreateIssueNormalizer()
    {
        var root = FindRepositoryRoot();
        var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = Path.Combine(
                root,
                "server",
                "Retrieval",
                "Resources",
                MaintenanceIssueLexiconOptions.LexiconFileName)
        });
        return new MaintenanceIssueNormalizer(loader);
    }

    private static DateTimeOffset AtManila(int year, int month, int day, int hour = 0, int minute = 0)
        => new(year, month, day, hour, minute, 0, TimeSpan.FromHours(8));

    private static string RequireSqlServerConnection()
        => Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")!;

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

    private sealed class TestContextFactory(string connectionString)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(connectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed record SearchFixtureRecord(
        Guid InspectionId,
        Guid AssetId,
        Guid ScheduleId,
        string AssetCode,
        string SearchText);

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
            var databaseName = $"UniPMSemanticTests_{Guid.NewGuid():N}";
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName
            };
            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };

            await using var connection = new SqlConnection(masterBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
            return new SqlServerTestDatabase(databaseBuilder.ConnectionString, databaseName);
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<bool> HasEmbeddingTableAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.tables
                WHERE name = N'MaintenanceSearchDocumentEmbeddings';
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        public async ValueTask DisposeAsync()
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
