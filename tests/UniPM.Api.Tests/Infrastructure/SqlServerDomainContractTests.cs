using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.PreventiveMaintenanceForms;
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

    [SqlServer2019Fact]
    public async Task Sql_Server_2019_with_full_text_search_applies_migrations_and_executes_containstable()
    {
        var baseConnectionString = RequireSqlServer2019Connection();
        await using (var server = new SqlConnection(baseConnectionString))
        {
            await server.OpenAsync();
            await using var command = server.CreateCommand();
            command.CommandText = "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion')), CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'));";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(15, reader.GetInt32(0));
            Assert.Equal(1, reader.GetInt32(1));
        }

        await using var database = await SqlServerTestDatabase.CreateAsync(baseConnectionString);
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(database.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{database.DatabaseName}] SET COMPATIBILITY_LEVEL = 150;";
            await command.ExecuteNonQueryAsync();
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var verification = new SqlConnection(database.ConnectionString);
        await verification.OpenAsync();
        await using var verificationCommand = verification.CreateCommand();
        verificationCommand.CommandText = """
            SELECT CONVERT(int, compatibility_level)
            FROM sys.databases
            WHERE name = DB_NAME();
            SELECT COUNT(*)
            FROM sys.fulltext_catalogs
            WHERE name = N'UniPMMaintenanceRetrieval';
            SELECT COUNT(*)
            FROM sys.fulltext_indexes AS indexTable
            INNER JOIN sys.tables AS tableInfo ON tableInfo.object_id = indexTable.object_id
            WHERE tableInfo.name = N'MaintenanceSearchDocuments' AND indexTable.is_enabled = 1;
            SELECT COUNT(*)
            FROM CONTAINSTABLE(dbo.MaintenanceSearchDocuments, SearchText, N'pressure');
            """;
        await using var verificationReader = await verificationCommand.ExecuteReaderAsync();
        Assert.True(await verificationReader.ReadAsync());
        Assert.Equal(150, verificationReader.GetInt32(0));
        Assert.True(await verificationReader.NextResultAsync());
        Assert.True(await verificationReader.ReadAsync());
        Assert.Equal(1, verificationReader.GetInt32(0));
        Assert.True(await verificationReader.NextResultAsync());
        Assert.True(await verificationReader.ReadAsync());
        Assert.Equal(1, verificationReader.GetInt32(0));
        Assert.True(await verificationReader.NextResultAsync());
        await verificationReader.ReadAsync();
    }

    [SqlServer2019Fact]
    public async Task Sql_Server_2019_creates_the_separate_reference_full_text_catalog()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONVERT(int, CASE WHEN EXISTS (
                SELECT 1
                FROM sys.fulltext_indexes AS fullTextIndex
                INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
                INNER JOIN sys.fulltext_catalogs AS catalog ON catalog.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
                WHERE tables.name = N'ReferenceDocumentSections'
                  AND catalog.name = N'UniPMReferenceRetrieval') THEN 1 ELSE 0 END);
            """;

        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [SqlServer2019Fact]
    public async Task Preventive_form_status_file_number_and_acknowledgement_constraints_are_enforced()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var formId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.PreventiveMaintenanceForms.AddRange(
                NewPreventiveForm(formId, "PMF-001", PreventiveMaintenanceFormStatusCatalog.Draft),
                NewPreventiveForm(Guid.NewGuid(), "PMF-002", PreventiveMaintenanceFormStatusCatalog.Submitted),
                NewPreventiveForm(Guid.NewGuid(), null, PreventiveMaintenanceFormStatusCatalog.Acknowledged));
            await context.SaveChangesAsync();

            context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
            {
                Id = Guid.NewGuid(),
                FormId = formId,
                SignatoryName = "Fictional Signatory",
                SignatoryPosition = "Department Head",
                CapturedByUserId = Guid.NewGuid(),
                AcknowledgedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        await AssertConstraintFailureAsync(database, NewPreventiveForm(
            Guid.NewGuid(), "PMF-001", PreventiveMaintenanceFormStatusCatalog.Draft));
        await AssertConstraintFailureAsync(database, NewPreventiveForm(
            Guid.NewGuid(), "PMF-003", "Completed"));
        var invalidAcademicYear = NewPreventiveForm(
            Guid.NewGuid(), "PMF-004", PreventiveMaintenanceFormStatusCatalog.Draft);
        invalidAcademicYear.AcademicYear = "2026/2027";
        await AssertConstraintFailureAsync(database, invalidAcademicYear);

        await using var acknowledgementContext = database.CreateContext();
        acknowledgementContext.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
        {
            Id = Guid.NewGuid(),
            FormId = formId,
            SignatoryName = "Another Fictional Signatory",
            SignatoryPosition = "Department Head",
            CapturedByUserId = Guid.NewGuid(),
            AcknowledgedAt = DateTimeOffset.UtcNow
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => acknowledgementContext.SaveChangesAsync());
    }

    [SqlServer2019Fact]
    public async Task Form_migration_preserves_existing_inspections_and_leaves_form_link_null()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServer2019Connection());
        var inspectionId = Guid.NewGuid();
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync("20260725082333_AddReferenceDocumentFoundation");
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "FORM-MIG-001",
                AssetCategory = "fire-alarm",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = now,
                PeriodType = "Semester",
                Status = "Due",
                Semester = "First",
                AcademicYear = "2026-2027",
                CreatedAt = now,
                UpdatedAt = now
            };
            var inspectorUserId = Guid.NewGuid();
            var dateInspected = now;
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Assets] ([Id], [AssetCode], [AssetCategory], [Status], [CreatedAt], [UpdatedAt])
                VALUES ({asset.Id}, {asset.AssetCode}, {asset.AssetCategory}, {asset.Status}, {asset.CreatedAt}, {asset.UpdatedAt});

                INSERT INTO [PreventiveMaintenanceSchedules]
                    ([Id], [AssetId], [ScheduleDate], [PeriodType], [Status], [Semester], [AcademicYear], [CreatedAt], [UpdatedAt])
                VALUES
                    ({schedule.Id}, {schedule.AssetId}, {schedule.ScheduleDate}, {schedule.PeriodType}, {schedule.Status},
                     {schedule.Semester}, {schedule.AcademicYear}, {schedule.CreatedAt}, {schedule.UpdatedAt});

                INSERT INTO [InspectionRecords]
                    ([Id], [ScheduleId], [AssetId], [InspectorUserId], [DateInspected], [IsOperational], [CreatedAt], [UpdatedAt])
                VALUES
                    ({inspectionId}, {schedule.Id}, {asset.Id}, {inspectorUserId}, {dateInspected}, {true}, {now}, {now});
                """);
            await context.Database.MigrateAsync();
        }

        await using var verification = database.CreateContext();
        var inspection = await verification.InspectionRecords.SingleAsync(item => item.Id == inspectionId);
        Assert.Null(inspection.PreventiveMaintenanceFormId);
        Assert.Equal(inspectionId, inspection.Id);
        Assert.Equal(1, await verification.InspectionRecords.CountAsync());
    }

    [SqlServerFact]
    public async Task Migration_preflight_preserves_line_order_when_canonicalizing_mixed_line_endings()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync(PreviousMigration);
            context.Assets.Add(new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = " fe\r\n\r\n001 ",
                AssetCategory = "fire-alarm",
                QrCodeValue = " qr\r0001 ",
                Status = "Active"
            });
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var verificationContext = database.CreateContext();
        var asset = await verificationContext.Assets.SingleAsync();
        Assert.Equal("FE 001", asset.AssetCode);
        Assert.Equal("QR 0001", asset.QrCodeValue);
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

    private static PreventiveMaintenanceForm NewPreventiveForm(Guid id, string? fileNumber, string status)
        => new()
        {
            Id = id,
            FileNumber = fileNumber,
            AssetCategory = "fire-alarm",
            PeriodType = "Semester",
            Semester = "First",
            AcademicYear = "2026-2027",
            Status = status,
            CreatedByUserId = Guid.NewGuid()
        };

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

    private static string RequireSqlServer2019Connection()
    {
        var connectionString = Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION");
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

        public string ConnectionString { get; }
        public string DatabaseName => databaseName;

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
                .UseUniPmSqlServer(ConnectionString)
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

internal sealed class SqlServer2019FactAttribute : FactAttribute
{
    public SqlServer2019FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNIPM_SQLSERVER2019_TEST_CONNECTION")))
        {
            Skip = "Set UNIPM_SQLSERVER2019_TEST_CONNECTION to run the SQL Server 2019 Full-Text compatibility test.";
        }
    }
}
