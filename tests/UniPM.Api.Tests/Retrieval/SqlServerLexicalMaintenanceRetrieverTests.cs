using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Retrieval;

public sealed class SqlServerLexicalMaintenanceRetrieverTests
{
    private const string PreviousMigration = "20260711001300_EnforceDomainContracts";
    private const string FullTextCatalogName = "UniPMMaintenanceRetrieval";

    [SqlServerFact]
    public async Task Migration_creates_and_removes_the_dedicated_full_text_schema()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.True(await database.HasFullTextSchemaAsync());

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
        }

        Assert.False(await database.HasFullTextSchemaAsync());
    }

    [SqlServerFact]
    public async Task Retrieval_supports_multilingual_prefix_filters_and_traceable_results()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var retriever = CreateRetriever(database);
        var lowPressure = await AddDocumentAsync(
            database,
            assetCategory: "fire-extinguisher",
            building: "Main Building",
            department: "Administration",
            location: "Ground Floor",
            isOperational: false,
            dateInspected: AtManila(2025, 5, 1),
            remarks: "low pressure",
            searchText: "remarks: low pressure");
        var emergencyLight = await AddDocumentAsync(
            database,
            assetCategory: "emergency-light",
            building: "Annex",
            department: "GSD",
            location: "East Hallway",
            isOperational: false,
            dateInspected: AtManila(2025, 6, 1),
            remarks: "hindi umiilaw",
            searchText: "remarks: hindi umiilaw");
        var alarm = await AddDocumentAsync(
            database,
            assetCategory: "fire-alarm",
            building: "Main Building",
            department: "Security",
            location: "Control Room",
            isOperational: true,
            dateInspected: AtManila(2025, 7, 1),
            remarks: "alarm panel fault",
            searchText: "remarks: alarm panel fault");

        await WaitForFullTextReadyAsync(database);

        var english = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("low pressure"),
            result => result.Any(item => item.InspectionId == lowPressure.InspectionId));
        var taglish = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("hindi umiilaw"),
            result => result.Any(item => item.InspectionId == emergencyLight.InspectionId));
        var prefix = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("press gaug"),
            result => result.Any(item => item.InspectionId == lowPressure.InspectionId));
        var categoryFiltered = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("fault", AssetCategory: " FIRE-ALARM "),
            result => result.Any(item => item.InspectionId == alarm.InspectionId));
        var operationalFiltered = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("alarm", IsOperational: true),
            result => result.Any(item => item.InspectionId == alarm.InspectionId));
        var dateFiltered = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest(
                "pressure",
                DateFrom: AtManila(2025, 5, 1),
                DateTo: AtManila(2025, 5, 1, 23, 59)),
            result => result.Any(item => item.InspectionId == lowPressure.InspectionId));
        var noResults = await retriever.SearchAsync(new LexicalMaintenanceSearchRequest("no-such-maintenance-term"));

        var result = Assert.Single(english);
        Assert.Equal(lowPressure.InspectionId, result.InspectionId);
        Assert.Equal(lowPressure.AssetId, result.AssetId);
        Assert.Equal(lowPressure.ScheduleId, result.ScheduleId);
        Assert.Equal(lowPressure.AssetCode, result.AssetCode);
        Assert.Equal(lowPressure.AssetCategory, result.AssetCategory);
        Assert.Equal(lowPressure.Building, result.Building);
        Assert.Equal(lowPressure.Department, result.Department);
        Assert.Equal(lowPressure.Location, result.Location);
        Assert.Equal(lowPressure.DateInspected, result.DateInspected);
        Assert.False(result.IsOperational);
        Assert.True(result.RawLexicalRank > 0);
        Assert.Equal("lexical", result.RetrievalChannel);
        Assert.Single(taglish);
        Assert.Single(prefix);
        Assert.Single(categoryFiltered);
        Assert.Single(operationalFiltered);
        Assert.Single(dateFiltered);
        Assert.Empty(noResults);
        Assert.Null(typeof(LexicalMaintenanceSearchResult).GetProperty("SearchText"));
        Assert.Null(typeof(LexicalMaintenanceSearchResult).GetProperty("IssueKeysJson"));
        Assert.Null(typeof(LexicalMaintenanceSearchResult).GetProperty("ScenarioTags"));
    }

    [SqlServerFact]
    public async Task Retrieval_uses_deterministic_rank_date_and_inspection_id_ordering()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var retriever = CreateRetriever(database);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await AddDocumentAsync(
            database,
            inspectionId: secondId,
            assetCategory: "fire-alarm",
            building: "Main Building",
            department: "Security",
            location: "Control Room",
            isOperational: false,
            dateInspected: AtManila(2025, 8, 1),
            remarks: "tie finding",
            searchText: "remarks: tie finding");
        await AddDocumentAsync(
            database,
            inspectionId: firstId,
            assetCategory: "fire-alarm",
            building: "Main Building",
            department: "Security",
            location: "Control Room",
            isOperational: false,
            dateInspected: AtManila(2025, 8, 1),
            remarks: "tie finding",
            searchText: "remarks: tie finding");

        await WaitForFullTextReadyAsync(database);
        var results = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("tie finding"),
            result => result.Count == 2);

        Assert.Equal(firstId, results[0].InspectionId);
        Assert.Equal(secondId, results[1].InspectionId);
    }

    [SqlServerFact]
    public async Task Updated_search_text_becomes_searchable_after_change_tracking()
    {
        await using var database = await CreateMigratedDatabaseAsync();
        var retriever = CreateRetriever(database);
        var document = await AddDocumentAsync(
            database,
            assetCategory: "water-drinking-station",
            building: "Main Building",
            department: "GSD",
            location: "Lobby",
            isOperational: false,
            dateInspected: AtManila(2025, 9, 1),
            remarks: "original finding",
            searchText: "remarks: original finding");

        await WaitForFullTextReadyAsync(database);
        await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("original finding"),
            result => result.Any(item => item.InspectionId == document.InspectionId));

        await using (var context = database.CreateContext())
        {
            var stored = await context.MaintenanceSearchDocuments
                .SingleAsync(item => item.InspectionId == document.InspectionId);
            stored.SearchText = "remarks: refreshed finding";
            stored.SourceUpdatedAt = AtManila(2025, 9, 2);
            await context.SaveChangesAsync();
        }

        var updated = await WaitForResultAsync(
            retriever,
            new LexicalMaintenanceSearchRequest("refreshed finding"),
            result => result.Any(item => item.InspectionId == document.InspectionId));

        Assert.Single(updated);
        var stale = await retriever.SearchAsync(new LexicalMaintenanceSearchRequest("original finding"));
        Assert.DoesNotContain(stale, item => item.InspectionId == document.InspectionId);
    }

    [SqlServerFact]
    public async Task Missing_full_text_index_fails_with_explicit_availability_error()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
        }

        var exception = await Assert.ThrowsAsync<LexicalMaintenanceAvailabilityException>(
            () => CreateRetriever(database).SearchAsync(new LexicalMaintenanceSearchRequest("pressure")));

        Assert.Contains("catalog or SearchText index", exception.Message, StringComparison.Ordinal);
    }

    private static async Task<SqlServerTestDatabase> CreateMigratedDatabaseAsync()
    {
        var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    private static SqlServerLexicalMaintenanceRetriever CreateRetriever(SqlServerTestDatabase database)
    {
        return new SqlServerLexicalMaintenanceRetriever(new TestContextFactory(database.ConnectionString));
    }

    private static async Task<SearchFixtureRecord> AddDocumentAsync(
        SqlServerTestDatabase database,
        string assetCategory,
        string building,
        string department,
        string location,
        bool isOperational,
        DateTimeOffset dateInspected,
        string remarks,
        string searchText,
        Guid? inspectionId = null)
    {
        var inspection = new SearchFixtureRecord(
            inspectionId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"TEST-{Guid.NewGuid():N}"[..13],
            assetCategory,
            building,
            department,
            location,
            dateInspected,
            isOperational);

        await using var context = database.CreateContext();
        context.Assets.Add(new Asset
        {
            Id = inspection.AssetId,
            AssetCode = inspection.AssetCode,
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
            Id = inspection.ScheduleId,
            AssetId = inspection.AssetId,
            ScheduleDate = dateInspected,
            PeriodType = "Quarter",
            Status = "Completed",
            CompletedAt = dateInspected,
            CreatedAt = dateInspected,
            UpdatedAt = dateInspected
        });
        context.InspectionRecords.Add(new InspectionRecord
        {
            Id = inspection.InspectionId,
            ScheduleId = inspection.ScheduleId,
            AssetId = inspection.AssetId,
            InspectorUserId = Guid.NewGuid(),
            DateInspected = dateInspected,
            IsOperational = isOperational,
            Remarks = remarks,
            CreatedAt = dateInspected,
            UpdatedAt = dateInspected
        });
        context.MaintenanceSearchDocuments.Add(new MaintenanceSearchDocument
        {
            InspectionId = inspection.InspectionId,
            AssetId = inspection.AssetId,
            ScheduleId = inspection.ScheduleId,
            AssetCode = inspection.AssetCode,
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

        await context.SaveChangesAsync();
        return inspection;
    }

    private static async Task<IReadOnlyList<LexicalMaintenanceSearchResult>> WaitForResultAsync(
        SqlServerLexicalMaintenanceRetriever retriever,
        LexicalMaintenanceSearchRequest request,
        Func<IReadOnlyList<LexicalMaintenanceSearchResult>, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                var results = await retriever.SearchAsync(request, timeout.Token);
                if (predicate(results))
                {
                    return results;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out while waiting for SQL Server Full-Text Search change tracking.");
        }
    }

    private static async Task WaitForFullTextReadyAsync(SqlServerTestDatabase database)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                if (await database.IsFullTextReadyAsync())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out while waiting for Full-Text catalog '{FullTextCatalogName}' population readiness.");
        }
    }

    private static DateTimeOffset AtManila(int year, int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8));
    }

    private static string RequireSqlServerConnection()
    {
        return Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")!;
    }

    private sealed class TestContextFactory(string connectionString)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
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
        string AssetCategory,
        string Building,
        string Department,
        string Location,
        DateTimeOffset DateInspected,
        bool IsOperational);

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
            var databaseName = $"UniPMLexicalTests_{Guid.NewGuid():N}";
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
                .UseSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<bool> HasFullTextSchemaAsync()
        {
            const string sql = """
                SELECT CONVERT(int,
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM sys.fulltext_indexes AS fullTextIndex
                        INNER JOIN sys.tables AS tables
                            ON tables.object_id = fullTextIndex.object_id
                        INNER JOIN sys.schemas AS schemas
                            ON schemas.schema_id = tables.schema_id
                        INNER JOIN sys.fulltext_catalogs AS catalog
                            ON catalog.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                        WHERE schemas.name = N'dbo'
                          AND tables.name = N'MaintenanceSearchDocuments'
                          AND catalog.name = N'UniPMMaintenanceRetrieval')
                    AND EXISTS (
                        SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'UniPMMaintenanceRetrieval')
                    THEN 1 ELSE 0 END);
                """;

            return Convert.ToInt32(await ExecuteScalarAsync(sql)) == 1;
        }

        public async Task<bool> IsFullTextReadyAsync()
        {
            const string sql = """
                SELECT CONVERT(int,
                    CASE WHEN ISNULL(FULLTEXTCATALOGPROPERTY(N'UniPMMaintenanceRetrieval', 'PopulateStatus'), -1) = 0
                    AND EXISTS (
                        SELECT 1 FROM sys.fulltext_indexes AS fullTextIndex
                        INNER JOIN sys.tables AS tables
                            ON tables.object_id = fullTextIndex.object_id
                        INNER JOIN sys.schemas AS schemas
                            ON schemas.schema_id = tables.schema_id
                        WHERE schemas.name = N'dbo'
                          AND tables.name = N'MaintenanceSearchDocuments')
                    THEN 1 ELSE 0 END);
                """;

            return Convert.ToInt32(await ExecuteScalarAsync(sql)) == 1;
        }

        private async Task<object?> ExecuteScalarAsync(string sql)
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            return await command.ExecuteScalarAsync();
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
