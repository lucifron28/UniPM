using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerPmAnalyticsTests
{
    private const string PmCycle = "2026-11";
    private static readonly DateTimeOffset ClosedPeriodNow =
        new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions WebJsonOptions =
        new(JsonSerializerDefaults.Web);

    [SqlServer2019Fact]
    public async Task Gsd_analytics_uses_sql_server_2019_metrics_and_enforces_access()
    {
        var baseConnectionString = RequireSqlServer2019Connection();
        await AssertSqlServer2019AndFullTextAsync(baseConnectionString);

        var database = await SqlServerPmAnalyticsDatabase.CreateAsync(baseConnectionString);
        string cancelledAssetCode;
        try
        {
            await using (var context = database.CreateContext())
            {
                await context.Database.MigrateAsync();
                cancelledAssetCode = await SeedScenarioAsync(context);
            }

            Assert.Equal(150, await database.GetCompatibilityLevelAsync());

            await using var gsdApplication = new SqlServerPmAnalyticsApplicationFactory(
                database.ConnectionString,
                ClosedPeriodNow,
                authenticated: true,
                AuthRoleCatalog.Gsd);
            using var gsdClient = gsdApplication.CreateClient();

            using var progressResponse = await QueryAsync(
                gsdClient,
                "Show progress for fire extinguishers in November 2026");
            Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
            var progressJson = await progressResponse.Content.ReadAsStringAsync();
            var progress = JsonSerializer.Deserialize<PmAnalyticsResponse>(
                progressJson,
                WebJsonOptions);
            Assert.NotNull(progress);
            Assert.Equal("Closed", progress.PeriodState);
            AssertMeasure(progress.Result, 2, 3, 66.67m, "Percent");
            Assert.Equal(3, progress.TotalSourceCount);
            Assert.Equal(3, progress.Sources.Count);
            Assert.DoesNotContain(
                progress.Sources,
                source => source.AssetCode == cancelledAssetCode);
            Assert.DoesNotContain("PRIVATE-REMARKS-MARKER", progressJson, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE-ACTIONS-MARKER", progressJson, StringComparison.Ordinal);
            Assert.DoesNotContain("\"remarks\"", progressJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"actionsRecommendations\"", progressJson, StringComparison.OrdinalIgnoreCase);

            var compliance = await QueryPayloadAsync(
                gsdClient,
                "Show on-time compliance for fire extinguishers in November 2026");
            AssertMeasure(compliance.Result, 1, 3, 33.33m, "Percent");

            var late = await QueryPayloadAsync(
                gsdClient,
                "Show late inspections for fire extinguishers in November 2026");
            AssertMeasure(late.Result, 1, 3, 1m, "Count");

            var nonOperational = await QueryPayloadAsync(
                gsdClient,
                "Show non-operational assets for fire extinguishers in November 2026");
            AssertMeasure(nonOperational.Result, 1, 3, 1m, "Count");

            foreach (var question in new[]
            {
                "Show late inspections for fire extinguishers in November 2026 department \"EMPTY\"",
                "Show non-operational assets for fire extinguishers in November 2026 department \"EMPTY\""
            })
            {
                var empty = await QueryPayloadAsync(gsdClient, question);
                Assert.Equal(0, empty.Result.Numerator);
                Assert.Equal(0, empty.Result.Denominator);
                Assert.Null(empty.Result.Value);
                Assert.Equal("Count", empty.Result.Unit);
                Assert.False(empty.Result.IsMeasurable);
            }

            using var unsupported = await QueryAsync(gsdClient, "Show progress for fire extinguishers last month");
            Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);

            await using var supervisorApplication = new SqlServerPmAnalyticsApplicationFactory(
                database.ConnectionString,
                ClosedPeriodNow,
                authenticated: true,
                AuthRoleCatalog.Supervisor);
            using var supervisorClient = supervisorApplication.CreateClient();
            using var forbidden = await QueryAsync(
                supervisorClient,
                "Show progress for fire extinguishers in November 2026");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

            await using var anonymousApplication = new SqlServerPmAnalyticsApplicationFactory(
                database.ConnectionString,
                ClosedPeriodNow,
                authenticated: false);
            using var anonymousClient = anonymousApplication.CreateClient();
            using var unauthorized = await QueryAsync(
                anonymousClient,
                "Show progress for fire extinguishers in November 2026");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }
        finally
        {
            await database.DisposeAsync();
        }

        await AssertDatabaseWasDroppedAsync(baseConnectionString, database.DatabaseName);
    }

    private static async Task<string> SeedScenarioAsync(ApplicationDbContext context)
    {
        var now = ClosedPeriodNow;
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var assetPrefix = $"NLA-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var assets = Enumerable.Range(1, 4)
            .Select(index => new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"{assetPrefix}-{index:D2}",
                AssetCategory = "fire-extinguisher",
                Department = "GSD",
                Building = "Synthetic Test Building",
                Location = "Synthetic Test Area",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            })
            .ToArray();
        var completionTimes = new DateTimeOffset?[]
        {
            deadline.AddTicks(-1),
            deadline.AddTicks(1),
            null,
            deadline
        };
        var scheduleStatuses = new[]
        {
            ScheduleStatusCatalog.Completed,
            ScheduleStatusCatalog.Completed,
            ScheduleStatusCatalog.Due,
            ScheduleStatusCatalog.Cancelled
        };
        var schedules = assets.Select((asset, index) => new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            ScheduleDate = deadline,
            PmCycle = PmCycle,
            PeriodType = "Quarter",
            Quarter = "Q4",
            Year = 2026,
            Status = scheduleStatuses[index],
            CompletedAt = completionTimes[index],
            CreatedAt = now,
            UpdatedAt = now
        }).ToArray();

        context.Users.Add(new ApplicationUser
        {
            Id = TestAuthenticationHandler.UserId,
            UserName = "sql-pm-analytics-inspector@unipm.local",
            NormalizedUserName = "SQL-PM-ANALYTICS-INSPECTOR@UNIPM.LOCAL",
            Email = "sql-pm-analytics-inspector@unipm.local",
            NormalizedEmail = "SQL-PM-ANALYTICS-INSPECTOR@UNIPM.LOCAL",
            EmailConfirmed = true,
            DisplayName = "Synthetic PM Analytics Inspector",
            IsActive = true
        });
        context.Assets.AddRange(assets);
        context.PreventiveMaintenanceSchedules.AddRange(schedules);
        context.InspectionRecords.AddRange(
            CreateInspection(schedules[0], assets[0], completionTimes[0]!.Value, true,
                "PRIVATE-REMARKS-MARKER", "PRIVATE-ACTIONS-MARKER"),
            CreateInspection(schedules[1], assets[1], completionTimes[1]!.Value, false,
                "PRIVATE-REMARKS-MARKER", "PRIVATE-ACTIONS-MARKER"),
            CreateInspection(schedules[3], assets[3], completionTimes[3]!.Value, true));
        await context.SaveChangesAsync();

        return assets[3].AssetCode;
    }

    private static InspectionRecord CreateInspection(
        PreventiveMaintenanceSchedule schedule,
        Asset asset,
        DateTimeOffset completedAt,
        bool isOperational,
        string? remarks = null,
        string? actionsRecommendations = null)
    {
        return new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            AssetId = asset.Id,
            InspectorUserId = TestAuthenticationHandler.UserId,
            DateInspected = completedAt,
            CompletedAt = completedAt,
            IsOperational = isOperational,
            Remarks = remarks,
            ActionsRecommendations = actionsRecommendations,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        };
    }

    private static void AssertMeasure(
        PmAnalyticsMeasureResponse measure,
        int numerator,
        int denominator,
        decimal value,
        string unit)
    {
        Assert.Equal(numerator, measure.Numerator);
        Assert.Equal(denominator, measure.Denominator);
        Assert.Equal(value, measure.Value);
        Assert.Equal(unit, measure.Unit);
        Assert.True(measure.IsMeasurable);
    }

    private static async Task AssertSqlServer2019AndFullTextAsync(string baseConnectionString)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'));";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(15, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    private static async Task<PmAnalyticsResponse> QueryPayloadAsync(
        HttpClient client,
        string question)
    {
        using var response = await QueryAsync(client, question);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PmAnalyticsResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private static Task<HttpResponseMessage> QueryAsync(HttpClient client, string question)
    {
        return client.PostAsJsonAsync("/api/v1/analytics/pm/query", new { question });
    }

    private static string RequireSqlServer2019Connection()
    {
        return Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION")!;
    }

    private static async Task AssertDatabaseWasDroppedAsync(
        string baseConnectionString,
        string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@databaseName);";
        command.Parameters.AddWithValue("@databaseName", databaseName);
        var databaseId = await command.ExecuteScalarAsync();
        Assert.True(databaseId is null || databaseId == DBNull.Value);
    }

    private sealed class SqlServerPmAnalyticsApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly DateTimeOffset now;
        private readonly bool authenticated;
        private readonly string[] roles;

        public SqlServerPmAnalyticsApplicationFactory(
            string connectionString,
            DateTimeOffset now,
            bool authenticated,
            params string[] roles)
        {
            this.connectionString = connectionString;
            this.now = now;
            this.authenticated = authenticated;
            this.roles = roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                if (authenticated)
                {
                    services.AddTestAuthentication(roles);
                }

                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseUniPmSqlServer(connectionString));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class SqlServerPmAnalyticsDatabase : IAsyncDisposable
    {
        private readonly string databaseName;

        private SqlServerPmAnalyticsDatabase(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            this.databaseName = databaseName;
        }

        public string ConnectionString { get; }
        public string DatabaseName => databaseName;

        public static async Task<SqlServerPmAnalyticsDatabase> CreateAsync(string baseConnectionString)
        {
            var databaseName = $"UniPMPmAnalytics_{Guid.NewGuid():N}";
            var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName
            };
            var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };
            var database = new SqlServerPmAnalyticsDatabase(
                databaseBuilder.ConnectionString,
                databaseName);
            var created = false;

            try
            {
                await using var connection = new SqlConnection(masterBuilder.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync();
                created = true;
                command.CommandText = $"ALTER DATABASE [{databaseName}] SET COMPATIBILITY_LEVEL = 150;";
                await command.ExecuteNonQueryAsync();
                return database;
            }
            catch
            {
                if (created)
                {
                    await database.DisposeAsync();
                }

                throw;
            }
        }

        public ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseUniPmSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task<int> GetCompatibilityLevelAsync()
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME();";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async ValueTask DisposeAsync()
        {
            using (var pooledConnection = new SqlConnection(ConnectionString))
            {
                SqlConnection.ClearPool(pooledConnection);
            }

            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
            await command.ExecuteNonQueryAsync();
        }
    }
}
