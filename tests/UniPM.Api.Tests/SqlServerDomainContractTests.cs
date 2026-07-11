using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerDomainContractTests
{
    private const string PreviousMigration = "20260710170229_AddMaintenanceSearchDocuments";

    [SqlServerFact]
    public async Task Migration_preflight_canonicalizes_existing_codes_before_constraints()
    {
        var baseConnectionString = RequireSqlServerConnection();

        await using var database = await SqlServerTestDatabase.CreateAsync(baseConnectionString);
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
            context.Assets.Add(new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = " fe-001 ",
                AssetCategory = " FIRE-ALARM ",
                QrCodeValue = " qr-001 ",
                Status = " active "
            });
            context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = context.Assets.Local.Single().Id,
                ScheduleDate = DateTimeOffset.UtcNow,
                PeriodType = " quarter ",
                Status = " due ",
                Quarter = " q1 ",
                Semester = " first ",
                AcademicYear = " 2025-2026 "
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var verificationContext = database.CreateContext();
        var asset = await verificationContext.Assets.SingleAsync();
        var schedule = await verificationContext.PreventiveMaintenanceSchedules.SingleAsync();

        Assert.Equal("FE-001", asset.AssetCode);
        Assert.Equal("fire-alarm", asset.AssetCategory);
        Assert.Equal("QR-001", asset.QrCodeValue);
        Assert.Equal("Active", asset.Status);
        Assert.Equal("Quarter", schedule.PeriodType);
        Assert.Equal("Due", schedule.Status);
        Assert.Equal("Q1", schedule.Quarter);
        Assert.Equal("First", schedule.Semester);
        Assert.Equal("2025-2026", schedule.AcademicYear);
    }

    [SqlServerFact]
    public async Task SqlServer_constraints_reject_invalid_codes_and_enforce_filtered_uniqueness()
    {
        var baseConnectionString = RequireSqlServerConnection();

        await using var database = await SqlServerTestDatabase.CreateAsync(baseConnectionString);
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Assets.Add(new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "DB-001",
                AssetCategory = "fire-alarm",
                Status = "Active",
                QrCodeValue = null
            });
            await context.SaveChangesAsync();
        }

        await AssertConstraintFailureAsync(database, new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "DB-001",
            AssetCategory = "fire-alarm",
            Status = "Active"
        });

        await AssertConstraintFailureAsync(database, new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "DB-002",
            AssetCategory = "unsupported-category",
            Status = "Active"
        });

        await AssertConstraintFailureAsync(database, new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = await GetFirstAssetIdAsync(database),
            ScheduleDate = DateTimeOffset.UtcNow,
            PeriodType = "Biweekly",
            Status = "Due"
        });

        await using var filteredIndexContext = database.CreateContext();
        filteredIndexContext.Assets.AddRange(
            new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "DB-003",
                AssetCategory = "fire-alarm",
                Status = "Active",
                QrCodeValue = null
            },
            new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "DB-004",
                AssetCategory = "fire-alarm",
                Status = "Active",
                QrCodeValue = null
            });
        await filteredIndexContext.SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task Migration_preflight_rejects_unsupported_overlength_and_canonical_duplicates()
    {
        var baseConnectionString = RequireSqlServerConnection();

        await AssertPreflightFailureAsync(
            baseConnectionString,
            context => context.Assets.Add(new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "DB-101",
                AssetCategory = "unsupported-category",
                Status = "Active"
            }),
            "unsupported code");

        await AssertPreflightFailureAsync(
            baseConnectionString,
            context => context.Assets.Add(new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "DB-102",
                AssetCategory = "fire-alarm",
                Status = "Active",
                Building = new string('x', 257)
            }),
            "exceeds its maximum length");

        await AssertPreflightFailureAsync(
            baseConnectionString,
            context => context.Assets.AddRange(
                new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = "DB-103",
                    AssetCategory = "fire-alarm",
                    Status = "Active"
                },
                new Asset
                {
                    Id = Guid.NewGuid(),
                    AssetCode = " db-103 ",
                    AssetCategory = "fire-alarm",
                    Status = "Active"
                }),
            "canonical asset codes are duplicated");
    }

    private static async Task<Guid> GetFirstAssetIdAsync(SqlServerTestDatabase database)
    {
        await using var context = database.CreateContext();
        return await context.Assets.Select(asset => asset.Id).SingleAsync();
    }

    private static async Task AssertConstraintFailureAsync(
        SqlServerTestDatabase database,
        object entity)
    {
        await using var context = database.CreateContext();
        context.Add(entity);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task AssertPreflightFailureAsync(
        string baseConnectionString,
        Action<ApplicationDbContext> addRecords,
        string expectedMessage)
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(baseConnectionString);
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(PreviousMigration);
        addRecords(context);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => context.Database.MigrateAsync());

        Assert.Contains(expectedMessage, exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string RequireSqlServerConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION");
        return connectionString!;
    }

    private sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private readonly string databaseName;

        private SqlServerTestDatabase(string connectionString, string databaseName)
        {
            ConnectionString = connectionString;
            this.databaseName = databaseName;
        }

        private string ConnectionString { get; }

        public static async Task<SqlServerTestDatabase> CreateAsync(string baseConnectionString)
        {
            var databaseName = $"UniPMContractTests_{Guid.NewGuid():N}";
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

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")))
        {
            Skip = "Set UNIPM_SQLSERVER_TEST_CONNECTION to run SQL Server migration and constraint tests.";
        }
    }
}
