using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Features.Reports;
using UniPM.Api.Models;
using Xunit.Abstractions;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3SqlServerTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(8));
    private readonly ITestOutputHelper output;

    public PmAnalyticsV3SqlServerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [SqlServer2019Fact]
    public async Task V3_quarter_drilldown_and_matching_denominators_translate_on_sql_server_2019()
    {
        var database = await DisposableDatabase.CreateAsync(RequireConnection());
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(database.ConnectionString)
                .Options;
            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.MigrateAsync();
                await SeedAsync(context);
            }

            Assert.Equal(150, await database.GetCompatibilityLevelAsync());
            Assert.StartsWith("15.", await database.GetProductVersionAsync(), StringComparison.Ordinal);

            var service = new PmAnalyticsV3QueryService(new SqlContextFactory(options), new FixedTimeProvider(AsOf));
            var q1 = await ExecuteRecordsAsync(service, quarter: 1);
            var q4 = await ExecuteRecordsAsync(service, quarter: 4);

            Assert.Equal(["2025-02"], q1.CyclesByCategory["fire-extinguisher"]);
            Assert.Equal("SQL-FE-FEB", Assert.Single(q1.Records).Values["assetCode"]);
            Assert.Equal(["2025-11"], q4.CyclesByCategory["fire-extinguisher"]);
            Assert.Equal("SQL-FE-NOV", Assert.Single(q4.Records).Values["assetCode"]);

            var aggregatePlan = new PmAnalyticsV3ReportPlan(
                3,
                PmAnalyticsV3Catalog.Version,
                PmAnalyticsV3Catalog.SubjectPmExecution,
                PmAnalyticsV3Mode.Aggregate,
                new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2025),
                ["fire-extinguisher", "fire-alarm"],
                [PmAnalyticsV3Catalog.CompletedCount],
                [PmAnalyticsV3Catalog.AssetCategory],
                [],
                [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])],
                [],
                [],
                null,
                PmAnalyticsV3Presentation.Table,
                [PmAnalyticsV3Catalog.AssetCategory, PmAnalyticsV3Catalog.CompletedCount]);
            var resolution = await service.NormalizeAndResolveAsync(aggregatePlan, CancellationToken.None);
            Assert.NotNull(resolution.Plan);
            var aggregate = await service.ExecuteAsync(resolution.Plan, 1, 100, CancellationToken.None);

            Assert.Equal(4, aggregate.Population.EligibleAssetCycleCount);
            Assert.Equal(3, aggregate.Population.MatchingAssetCycleCount);
            var extinguisher = aggregate.AggregateRows.Single(row => Equals(row.Values[PmAnalyticsV3Catalog.AssetCategory], "fire-extinguisher"));
            Assert.Equal(3, extinguisher.Measures[PmAnalyticsV3Catalog.CompletedCount].Numerator);
            Assert.Equal(3, extinguisher.Measures[PmAnalyticsV3Catalog.CompletedCount].Denominator);
            var alarm = aggregate.AggregateRows.Single(row => Equals(row.Values[PmAnalyticsV3Catalog.AssetCategory], "fire-alarm"));
            Assert.Equal(0, alarm.Measures[PmAnalyticsV3Catalog.CompletedCount].Numerator);
            Assert.Equal(1, alarm.Measures[PmAnalyticsV3Catalog.CompletedCount].Denominator);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServer2019Fact]
    public async Task Inventory_null_location_filter_counts_assets_without_schedule_joins_on_sql_server_2019()
    {
        var database = await DisposableDatabase.CreateAsync(RequireConnection());
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(database.ConnectionString)
                .Options;
            await using (var context = new ApplicationDbContext(options))
            {
                await context.Database.MigrateAsync();
                context.Assets.AddRange(
                    CreateInventoryAsset("SQL-INVENTORY-NULL", null),
                    CreateInventoryAsset("SQL-INVENTORY-BLANK", "   "),
                    CreateInventoryAsset("SQL-INVENTORY-ROOM", "SQL TEST ROOM"));
                await context.SaveChangesAsync();
            }

            Assert.Equal(150, await database.GetCompatibilityLevelAsync());
            Assert.StartsWith("15.", await database.GetProductVersionAsync(), StringComparison.Ordinal);

            var service = new PmAnalyticsV3QueryService(new SqlContextFactory(options), new FixedTimeProvider(AsOf));
            var plan = new PmAnalyticsV3ReportPlan(
                3,
                PmAnalyticsV3Catalog.Version,
                PmAnalyticsV3Catalog.SubjectAssetInventory,
                PmAnalyticsV3Mode.Aggregate,
                null,
                ["fire-extinguisher"],
                [PmAnalyticsV3Catalog.RegisteredAssetCount],
                [PmAnalyticsV3Catalog.Location],
                [
                    new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Department, PmAnalyticsV3FilterOperator.Equal, ["SQL INVENTORY"]),
                    new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Location, PmAnalyticsV3FilterOperator.IsNull)
                ],
                [],
                [],
                [],
                null,
                PmAnalyticsV3Presentation.Table,
                [PmAnalyticsV3Catalog.Location, PmAnalyticsV3Catalog.RegisteredAssetCount]);
            var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
            Assert.NotNull(resolution.Plan);
            var execution = await service.ExecuteAsync(resolution.Plan, 1, 25, CancellationToken.None);

            Assert.Equal(2, execution.Population.SourceTotalCount);
            var row = Assert.Single(execution.AggregateRows);
            Assert.Null(row.Values[PmAnalyticsV3Catalog.Location]);
            Assert.Equal(2, row.Measures[PmAnalyticsV3Catalog.RegisteredAssetCount].Value);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [SqlServer2019Fact]
    public async Task Sql_server_2019_synthetic_pm_reports_measure_100_1000_and_10000_rows()
    {
        await using var database = await DisposableDatabase.CreateAsync(RequireConnection());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseUniPmSqlServer(database.ConnectionString)
            .Options;
        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.MigrateAsync();
            AddScaleRows(context, "fire-extinguisher", "2025-02", "FE", 100);
            AddScaleRows(context, "fire-alarm", "2025-06", "FA", 1_000);
            AddScaleRows(context, "emergency-light", "2025-06", "EL", 10_000);
            await context.SaveChangesAsync();
        }

        Assert.Equal(150, await database.GetCompatibilityLevelAsync());
        Assert.StartsWith("15.", await database.GetProductVersionAsync(), StringComparison.Ordinal);
        var service = new PmAnalyticsV3QueryService(new SqlContextFactory(options), new FixedTimeProvider(AsOf));
        foreach (var sample in new[]
        {
            (Category: "fire-extinguisher", ExpectedRows: 100),
            (Category: "fire-alarm", ExpectedRows: 1_000),
            (Category: "emergency-light", ExpectedRows: 10_000)
        })
        {
            var plan = new PmAnalyticsV3ReportPlan(
                3,
                PmAnalyticsV3Catalog.Version,
                PmAnalyticsV3Catalog.SubjectPmExecution,
                PmAnalyticsV3Mode.Aggregate,
                new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2025),
                [sample.Category],
                [PmAnalyticsV3Catalog.ScheduledCount],
                [],
                [],
                [],
                [],
                [],
                null,
                PmAnalyticsV3Presentation.Table,
                [PmAnalyticsV3Catalog.ScheduledCount]);
            var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
            Assert.NotNull(resolution.Plan);
            var stopwatch = Stopwatch.StartNew();
            var result = await service.ExecuteAsync(resolution.Plan, 1, 25, CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(sample.ExpectedRows, result.Population.EligibleAssetCycleCount);
            Assert.Equal(sample.ExpectedRows, Assert.Single(result.AggregateRows)
                .Measures[PmAnalyticsV3Catalog.ScheduledCount].Numerator);
            output.WriteLine(
                $"sql2019 synthetic pmExecution category={sample.Category} eligibleRows={sample.ExpectedRows} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F2} compatibility=150");
        }
    }

    private static async Task<PmAnalyticsV3ExecutionResponse> ExecuteRecordsAsync(
        PmAnalyticsV3QueryService service,
        int quarter)
    {
        var plan = new PmAnalyticsV3ReportPlan(
            3,
            PmAnalyticsV3Catalog.Version,
            PmAnalyticsV3Catalog.SubjectPmExecution,
            PmAnalyticsV3Mode.Records,
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Quarter, Year: 2025, Quarter: quarter),
            ["fire-extinguisher"],
            [],
            [],
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Building, PmAnalyticsV3FilterOperator.Equal, ["NORTH"])],
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])],
            [],
            [],
            null,
            PmAnalyticsV3Presentation.Table,
            ["assetCode", PmAnalyticsV3Catalog.AssetCategory, PmAnalyticsV3Catalog.Building, PmAnalyticsV3Catalog.PmCycle, PmAnalyticsV3Catalog.Condition]);
        var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
        Assert.NotNull(resolution.Plan);
        return await service.ExecuteAsync(resolution.Plan, 1, 100, CancellationToken.None);
    }

    private static async Task SeedAsync(ApplicationDbContext context)
    {
        var inspectorId = Guid.NewGuid();
        context.Users.Add(new ApplicationUser
        {
            Id = inspectorId,
            UserName = $"nla-v3-sql-{inspectorId:N}@unipm.local",
            NormalizedUserName = $"NLA-V3-SQL-{inspectorId:N}@UNIPM.LOCAL",
            Email = $"nla-v3-sql-{inspectorId:N}@unipm.local",
            NormalizedEmail = $"NLA-V3-SQL-{inspectorId:N}@UNIPM.LOCAL",
            EmailConfirmed = true,
            DisplayName = "Synthetic V3 SQL test inspector",
            IsActive = true
        });

        AddAssetCycle(context, inspectorId, "SQL-FE-FEB", "fire-extinguisher", "2025-02", false);
        AddAssetCycle(context, inspectorId, "SQL-FE-MAY", "fire-extinguisher", "2025-05", false);
        AddAssetCycle(context, inspectorId, "SQL-FE-NOV", "fire-extinguisher", "2025-11", false);
        AddAssetCycle(context, inspectorId, "SQL-FA-JUN", "fire-alarm", "2025-06", true);
        await context.SaveChangesAsync();
    }

    private static void AddAssetCycle(
        ApplicationDbContext context,
        Guid inspectorId,
        string assetCode,
        string category,
        string cycle,
        bool operational)
    {
        var year = int.Parse(cycle[..4], System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(cycle[5..], System.Globalization.CultureInfo.InvariantCulture);
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = category,
            Department = "SQL TEST OPERATIONS",
            Building = "NORTH",
            Location = "SQL TEST ROOM",
            Status = AssetStatusCatalog.Active
        };
        var schedule = new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            PmCycle = cycle,
            ScheduleDate = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Completed
        };
        var inspection = new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            Schedule = schedule,
            AssetId = asset.Id,
            Asset = asset,
            InspectorUserId = inspectorId,
            DateInspected = new DateTimeOffset(year, month, 15, 12, 0, 0, TimeSpan.FromHours(8)),
            CompletedAt = new DateTimeOffset(year, month, 15, 12, 0, 0, TimeSpan.FromHours(8)),
            IsOperational = operational
        };
        context.Assets.Add(asset);
        context.PreventiveMaintenanceSchedules.Add(schedule);
        context.InspectionRecords.Add(inspection);
    }

    private static Asset CreateInventoryAsset(string code, string? location) => new()
    {
        Id = Guid.NewGuid(),
        AssetCode = code,
        AssetCategory = "fire-extinguisher",
        Department = "SQL INVENTORY",
        Building = "SQL INVENTORY BUILDING",
        Location = location,
        Status = AssetStatusCatalog.Active
    };

    private static void AddScaleRows(
        ApplicationDbContext context,
        string category,
        string cycle,
        string prefix,
        int count)
    {
        var month = int.Parse(cycle[^2..], System.Globalization.CultureInfo.InvariantCulture);
        var assets = new List<Asset>(count);
        var schedules = new List<PreventiveMaintenanceSchedule>(count);
        for (var index = 0; index < count; index++)
        {
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"SQLSCALE-{prefix}-{index:D6}",
                AssetCategory = category,
                Department = "SQL SYNTHETIC SCALE",
                Building = "SQL SYNTHETIC BUILDING",
                Location = "SQL SYNTHETIC ROOM",
                Status = AssetStatusCatalog.Active
            };
            assets.Add(asset);
            schedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Asset = asset,
                PmCycle = cycle,
                ScheduleDate = new DateTimeOffset(2025, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                PeriodType = "Quarter",
                Status = ScheduleStatusCatalog.Due
            });
        }

        context.Assets.AddRange(assets);
        context.PreventiveMaintenanceSchedules.AddRange(schedules);
    }

    private static string RequireConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set UNIPM_SQLSERVER2019_TEST_CONNECTION to a disposable local SQL Server 2019 instance.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        var server = builder.DataSource.Trim();
        if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            server = server[4..];
        }

        var host = server.Split(',', StringSplitOptions.TrimEntries)[0].Trim('[', ']');
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !host.Equals("127.0.0.1", StringComparison.Ordinal)
            && !host.Equals("::1", StringComparison.Ordinal)
            && !host.Equals(".", StringComparison.Ordinal)
            && !host.Equals("(local)", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The V3 SQL integration test only accepts a disposable loopback SQL Server; remote SQL hosts are rejected.");
        }

        return builder.ConnectionString;
    }

    private sealed class SqlContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DisposableDatabase : IAsyncDisposable
    {
        private readonly string databaseName;

        private DisposableDatabase(string databaseName, string connectionString)
        {
            this.databaseName = databaseName;
            ConnectionString = connectionString;
        }

        internal string ConnectionString { get; }

        internal static async Task<DisposableDatabase> CreateAsync(string serverConnection)
        {
            var databaseName = $"UniPMNlaV3_{Guid.NewGuid():N}";
            var target = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = databaseName };
            var master = new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = "master" };
            var database = new DisposableDatabase(databaseName, target.ConnectionString);
            var created = false;
            try
            {
                await using var connection = new SqlConnection(master.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync();
                created = true;
                command.CommandText = $"ALTER DATABASE [{databaseName}] SET COMPATIBILITY_LEVEL = 150";
                await command.ExecuteNonQueryAsync();
                return database;
            }
            catch
            {
                if (created) await database.DisposeAsync();
                throw;
            }
        }

        internal async Task<int> GetCompatibilityLevelAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        internal async Task<string> GetProductVersionAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion'))";
            return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            using (var pooledConnection = new SqlConnection(ConnectionString))
            {
                SqlConnection.ClearPool(pooledConnection);
            }

            var master = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
            await using var connection = new SqlConnection(master.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
