using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerInspectionSubmissionIntegrityTests
{
    private const string PreviousMigration = "20260713001356_AddIdentityAuthentication";

    [SqlServerFact]
    public async Task Migration_preflight_rejects_duplicate_inspections_for_one_schedule()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(PreviousMigration);

        var schedule = AddAssetAndSchedule(context);
        context.InspectionRecords.AddRange(
            CreateInspection(schedule),
            CreateInspection(schedule));
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync());

        Assert.Contains(
            "Inspection integrity migration stopped: multiple inspection records exist for one schedule.",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [SqlServerFact]
    public async Task Unique_index_rejects_duplicate_inspections_for_one_schedule()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        Guid scheduleId;

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            var schedule = AddAssetAndSchedule(context);
            await context.SaveChangesAsync();
            scheduleId = schedule.Id;

            context.InspectionRecords.Add(CreateInspection(schedule));
            await context.SaveChangesAsync();
        }

        await using (var duplicateContext = database.CreateContext())
        {
            var schedule = await duplicateContext.PreventiveMaintenanceSchedules.SingleAsync();
            duplicateContext.InspectionRecords.Add(CreateInspection(schedule));

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(1, await verificationContext.InspectionRecords.CountAsync(inspection => inspection.ScheduleId == scheduleId));
    }

    [SqlServerFact]
    public async Task Concurrent_endpoint_submissions_create_one_inspection_and_one_search_document()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlServerInspectionApplicationFactory(database.ConnectionString);
        var scheduleId = await application.SeedAsync();
        using var firstClient = application.CreateClient();
        using var secondClient = application.CreateClient();
        var dateInspected = new DateTimeOffset(2026, 7, 17, 9, 0, 0, TimeSpan.FromHours(8));

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/inspections/", CreateRequest(scheduleId, dateInspected)),
            secondClient.PostAsJsonAsync("/api/v1/inspections/", CreateRequest(scheduleId, dateInspected)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));

        await using var context = database.CreateContext();
        var inspection = await context.InspectionRecords.SingleAsync(record => record.ScheduleId == scheduleId);
        var document = await context.MaintenanceSearchDocuments.SingleAsync();
        var schedule = await context.PreventiveMaintenanceSchedules.SingleAsync(candidate => candidate.Id == scheduleId);

        Assert.Equal(inspection.Id, document.InspectionId);
        Assert.Equal("Completed", schedule.Status);
        Assert.NotNull(schedule.CompletedAt);
    }

    private static object CreateRequest(Guid scheduleId, DateTimeOffset dateInspected) => new
    {
        scheduleId,
        inspectorUserId = TestAuthenticationHandler.UserId,
        dateInspected,
        isOperational = true,
        remarks = "Concurrent SQL Server submission"
    };

    private static PreventiveMaintenanceSchedule AddAssetAndSchedule(ApplicationDbContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = $"SQL-{Guid.NewGuid():N}"[..20],
            AssetCategory = "fire-extinguisher",
            Building = "Test Building",
            Department = "GSD",
            Location = "Test Room",
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        };
        var schedule = new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            ScheduleDate = now,
            PeriodType = "Quarter",
            Status = "Due",
            Quarter = "Q1",
            Year = 2026,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Assets.Add(asset);
        context.PreventiveMaintenanceSchedules.Add(schedule);
        return schedule;
    }

    private static InspectionRecord CreateInspection(PreventiveMaintenanceSchedule schedule)
    {
        var now = DateTimeOffset.UtcNow;
        return new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            AssetId = schedule.AssetId,
            InspectorUserId = TestAuthenticationHandler.UserId,
            DateInspected = now,
            IsOperational = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static string RequireSqlServerConnection()
    {
        return Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")!;
    }

    private sealed class SqlServerInspectionApplicationFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(AuthRoleCatalog.Inspector);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
            });
        }

        public async Task<Guid> SeedAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "sql-inspector@unipm.local",
                NormalizedUserName = "SQL-INSPECTOR@UNIPM.LOCAL",
                Email = "sql-inspector@unipm.local",
                NormalizedEmail = "SQL-INSPECTOR@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "SQL Inspector",
                IsActive = true
            });
            var schedule = AddAssetAndSchedule(context);
            await context.SaveChangesAsync();
            return schedule.Id;
        }
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
            var databaseName = $"UniPMInspectionIntegrity_{Guid.NewGuid():N}";
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

        public async ValueTask DisposeAsync()
        {
            var masterBuilder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            await using var connection = new SqlConnection(masterBuilder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }
    }
}
