using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerPhotoSubmissionRaceTests
{
    private const string MigrationBeforePhotoEvidence = "20261009020518_AddUniqueScheduleAssetCycle";

    [SqlServerFact]
    public async Task Photo_evidence_migration_up_down_up_preserves_inspection_and_gps_records()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var storage = new GatedPhotoStorage(releaseImmediately: true);
        await using var application = new SqlServerPhotoApplicationFactory(database.ConnectionString, storage);
        var scenario = await application.SeedCompletedDraftAsync(
            targetMigration: MigrationBeforePhotoEvidence,
            includeGpsHistory: true);

        await using (var context = database.CreateContext())
        {
            await AssertSqlServer2019Compatibility150Async(context);
            await AssertInspectionAndGpsHistoryAsync(context, scenario, expectPhotoTable: false);
            await context.Database.MigrateAsync();
            await AssertInspectionAndGpsHistoryAsync(context, scenario, expectPhotoTable: true);

            await context.Database.MigrateAsync(MigrationBeforePhotoEvidence);
            await AssertInspectionAndGpsHistoryAsync(context, scenario, expectPhotoTable: false);

            await context.Database.MigrateAsync();
            await AssertInspectionAndGpsHistoryAsync(context, scenario, expectPhotoTable: true);
        }
    }

    [SqlServerFact]
    public async Task SqlServer_photo_lifecycle_enforces_one_row_and_keeps_authorization_and_rollback()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var storage = new GatedPhotoStorage(releaseImmediately: true);
        await using var application = new SqlServerPhotoApplicationFactory(database.ConnectionString, storage);
        using var client = application.CreateClient();
        var scenario = await application.SeedCompletedDraftAsync();

        await using (var gsdApplication = new SqlServerPhotoApplicationFactory(
            database.ConnectionString,
            storage,
            role: AuthRoleCatalog.Gsd))
        using (var gsdClient = gsdApplication.CreateClient())
        {
            using var unauthorizedContent = new ByteArrayContent(MinimalJpeg());
            unauthorizedContent.Headers.ContentType = new("image/jpeg");
            var unauthorizedUpload = await gsdClient.PutAsync(
                $"/api/v1/inspections/{scenario.InspectionId}/photo",
                unauthorizedContent);
            Assert.Equal(HttpStatusCode.Forbidden, unauthorizedUpload.StatusCode);
        }

        using var firstContent = new ByteArrayContent(MinimalJpeg());
        firstContent.Headers.ContentType = new("image/jpeg");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            firstContent)).StatusCode);
        var firstEvidence = await application.ReadEvidenceAsync(scenario.InspectionId);
        Assert.NotNull(firstEvidence);

        using (var duplicateContext = database.CreateContext())
        {
            var duplicateInsert = duplicateContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [dbo].[InspectionPhotoEvidence]
                    ([InspectionId], [StorageKey], [LengthBytes], [UploadedAt], [Revision])
                VALUES
                    ({scenario.InspectionId}, {Guid.NewGuid():N}.jpg, {MinimalJpeg().Length}, {DateTimeOffset.UtcNow}, 1)
                """);
            await Assert.ThrowsAsync<SqlException>(() => duplicateInsert);
        }

        using var replacementContent = new ByteArrayContent(MinimalJpeg());
        replacementContent.Headers.ContentType = new("image/jpeg");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            replacementContent)).StatusCode);
        var replacement = await application.ReadEvidenceAsync(scenario.InspectionId);
        Assert.NotNull(replacement);
        Assert.Equal(2, replacement.Revision);
        Assert.Single(storage.Files);

        var read = await client.GetAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);

        var delete = await client.DeleteAsync($"/api/v1/inspections/{scenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Null(await application.ReadEvidenceAsync(scenario.InspectionId));
        Assert.Empty(storage.Files);

        await using (var context = database.CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER [dbo].[TR_TestRejectInspectionPhotoInsert]
                ON [dbo].[InspectionPhotoEvidence]
                AFTER INSERT
                AS
                BEGIN
                    THROW 51000, 'Controlled SQL photo rollback test.', 1;
                END;
                """);
        }

        using var rollbackContent = new ByteArrayContent(MinimalJpeg());
        rollbackContent.Headers.ContentType = new("image/jpeg");
        var rollbackResponse = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            rollbackContent);
        Assert.Equal(HttpStatusCode.InternalServerError, rollbackResponse.StatusCode);
        Assert.Null(await application.ReadEvidenceAsync(scenario.InspectionId));
        Assert.Empty(storage.Files);
    }

    [SqlServerFact]
    public async Task Submission_wins_when_it_commits_while_photo_storage_is_in_progress()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var storage = new GatedPhotoStorage();
        await using var application = new SqlServerPhotoApplicationFactory(database.ConnectionString, storage);
        using var client = application.CreateClient();
        var scenario = await application.SeedCompletedDraftAsync();

        using var content = new ByteArrayContent(MinimalJpeg());
        content.Headers.ContentType = new("image/jpeg");
        var uploadTask = client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            content);
        await storage.WaitForSaveAsync();

        HttpResponseMessage submission;
        try
        {
            submission = await client.PostAsync(
                $"/api/v1/preventive-maintenance-forms/{scenario.FormId}/submit",
                content: null);
        }
        finally
        {
            storage.ReleaseSave();
        }

        using (submission)
        using (var upload = await uploadTask)
        {
            Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, upload.StatusCode);
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            PreventiveMaintenanceFormStatusCatalog.Submitted,
            await verificationContext.PreventiveMaintenanceForms
                .Where(form => form.Id == scenario.FormId)
                .Select(form => form.Status)
                .SingleAsync());
        Assert.False(await verificationContext.InspectionPhotoEvidence
            .AnyAsync(photo => photo.InspectionId == scenario.InspectionId));
        Assert.Empty(storage.Files);
    }

    [SqlServerFact]
    public async Task Photo_upload_wins_before_submission_and_remains_attached_after_submission()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var insertGate = new PhotoInsertGate();
        var storage = new GatedPhotoStorage(releaseImmediately: true);
        await using var application = new SqlServerPhotoApplicationFactory(
            database.ConnectionString,
            storage,
            insertGate);
        using var client = application.CreateClient();
        var scenario = await application.SeedCompletedDraftAsync();

        using var content = new ByteArrayContent(MinimalJpeg());
        content.Headers.ContentType = new("image/jpeg");
        var uploadTask = client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            content);
        await insertGate.WaitForInsertAsync();

        var submissionTask = client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{scenario.FormId}/submit",
            content: null);
        try
        {
            await WaitForSqlLockWaitAsync(database.ConnectionString, submissionTask);
        }
        finally
        {
            insertGate.ReleaseInsert();
        }

        using (var upload = await uploadTask)
        using (var submission = await submissionTask)
        {
            Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);
            Assert.Equal(HttpStatusCode.OK, submission.StatusCode);
        }

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            PreventiveMaintenanceFormStatusCatalog.Submitted,
            await verificationContext.PreventiveMaintenanceForms
                .Where(form => form.Id == scenario.FormId)
                .Select(form => form.Status)
                .SingleAsync());
        Assert.True(await verificationContext.InspectionPhotoEvidence
            .AnyAsync(photo => photo.InspectionId == scenario.InspectionId));
        Assert.Single(storage.Files);

        using var lateUploadContent = new ByteArrayContent(MinimalJpeg());
        lateUploadContent.Headers.ContentType = new("image/jpeg");
        var lateUpload = await client.PutAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo",
            lateUploadContent);
        var lateDelete = await client.DeleteAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/photo");
        Assert.Equal(HttpStatusCode.Conflict, lateUpload.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lateDelete.StatusCode);
        Assert.True(await verificationContext.InspectionPhotoEvidence
            .AnyAsync(photo => photo.InspectionId == scenario.InspectionId));
    }

    private static async Task AssertSqlServer2019Compatibility150Async(ApplicationDbContext context)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int), compatibility_level
            FROM sys.databases
            WHERE name = DB_NAME();
            """;
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(15, Convert.ToInt32(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(150, Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task AssertInspectionAndGpsHistoryAsync(
        ApplicationDbContext context,
        PhotoScenario scenario,
        bool expectPhotoTable)
    {
        var hasPhotoTable = await context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value]
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'InspectionPhotoEvidence'
            """).SingleAsync() == 1;
        Assert.Equal(expectPhotoTable, hasPhotoTable);

        var inspection = await context.InspectionRecords.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.InspectionId);
        Assert.Equal(scenario.LocationAttemptId, inspection.LocationAttemptId);
        Assert.Equal("Preserve this inspection across photo migration rollback.", inspection.Remarks);
        var attempt = await context.InspectionLocationAttempts.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.LocationAttemptId);
        Assert.Equal(14.5995, attempt.MeasuredLatitude);
        Assert.Equal(120.9842, attempt.MeasuredLongitude);
        Assert.Equal("Inside", attempt.Outcome);
        var asset = await context.Assets.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == scenario.AssetId);
        Assert.Equal(14.5995, asset.VerificationLatitude);
        Assert.Equal(120.9842, asset.VerificationLongitude);
    }

    private static async Task WaitForSqlLockWaitAsync(
        string connectionString,
        Task<HttpResponseMessage> competingRequest)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.dm_exec_requests
            WHERE database_id = DB_ID()
                AND wait_type LIKE N'LCK_M_%';
            """;
        var timeout = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeout)
        {
            if (Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0)
            {
                return;
            }

            if (competingRequest.IsCompleted)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        var state = competingRequest.IsCompletedSuccessfully
            ? $"HTTP {(int)competingRequest.Result.StatusCode}"
            : competingRequest.Status.ToString();
        throw new TimeoutException(
            $"The form submission did not wait on the photo transaction's SQL Server lock; request={state}.");
    }

    private static byte[] MinimalJpeg() =>
    [
        0xff, 0xd8,
        0xff, 0xc0, 0x00, 0x0b, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
        0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00,
        0x11,
        0xff, 0xd9
    ];

    private static string RequireSqlServerConnection() =>
        Environment.GetEnvironmentVariable("UNIPM_SQLSERVER_TEST_CONNECTION")!;

    private sealed class SqlServerPhotoApplicationFactory(
        string connectionString,
        GatedPhotoStorage storage,
        DbCommandInterceptor? commandInterceptor = null,
        string role = AuthRoleCatalog.Inspector) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(role);
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                {
                    options.UseUniPmSqlServer(connectionString);
                    if (commandInterceptor is not null)
                    {
                        options.AddInterceptors(commandInterceptor);
                    }
                });
                services.RemoveAll<IInspectionPhotoEvidenceStorage>();
                services.AddSingleton<IInspectionPhotoEvidenceStorage>(storage);
            });
        }

        public async Task<PhotoScenario> SeedCompletedDraftAsync(
            string? targetMigration = null,
            bool includeGpsHistory = false)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.MigrateAsync(targetMigration);

            var now = DateTimeOffset.UtcNow;
            var user = new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "sql-photo-race@unipm.local",
                NormalizedUserName = "SQL-PHOTO-RACE@UNIPM.LOCAL",
                Email = "sql-photo-race@unipm.local",
                NormalizedEmail = "SQL-PHOTO-RACE@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "SQL Photo Race Inspector",
                IsActive = true
            };
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"PHOTO-RACE-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
                AssetCategory = "fire-extinguisher",
                Building = "Photo Race Test Building",
                Department = "GSD",
                Location = "Photo Race Test Room",
                VerificationLatitude = includeGpsHistory ? 14.5995 : null,
                VerificationLongitude = includeGpsHistory ? 120.9842 : null,
                VerificationRadiusMeters = includeGpsHistory ? 100 : null,
                Status = AssetStatusCatalog.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            const string pmCycle = "2026-11";
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Asset = asset,
                ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
                PmCycle = pmCycle,
                PeriodType = "Quarter",
                Quarter = "Q4",
                Year = 2026,
                Status = ScheduleStatusCatalog.Completed,
                CompletedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = asset.AssetCategory,
                Building = asset.Building,
                Department = asset.Department,
                PmCycle = schedule.PmCycle,
                PeriodType = schedule.PeriodType,
                Quarter = schedule.Quarter,
                Year = schedule.Year,
                Status = PreventiveMaintenanceFormStatusCatalog.Draft,
                CreatedByUserId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                PreventiveMaintenanceFormId = form.Id,
                AssetId = asset.Id,
                InspectorUserId = user.Id,
                DateInspected = now,
                CompletedAt = now,
                IsOperational = true,
                Remarks = includeGpsHistory
                    ? "Preserve this inspection across photo migration rollback."
                    : "Concurrent photo lifecycle test.",
                CreatedAt = now,
                UpdatedAt = now
            };

            Guid? locationAttemptId = null;
            if (includeGpsHistory)
            {
                var locationAttempt = new InspectionLocationAttempt
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    ScheduleId = schedule.Id,
                    ActorUserId = user.Id,
                    CapturedAt = now,
                    DevicePositionTimestamp = now,
                    MeasuredLatitude = 14.5995,
                    MeasuredLongitude = 120.9842,
                    AccuracyMeters = 13,
                    HasAccuracy = true,
                    AccuracyMode = "Precise",
                    AcquisitionDurationMs = 120,
                    ExpectedLatitude = 14.5995,
                    ExpectedLongitude = 120.9842,
                    ExpectedRadiusMeters = 100,
                    DistanceMeters = 0,
                    Outcome = "Inside"
                };
                locationAttemptId = locationAttempt.Id;
                inspection.LocationAttemptId = locationAttempt.Id;
                context.InspectionLocationAttempts.Add(locationAttempt);
            }

            context.Users.Add(user);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.PreventiveMaintenanceForms.Add(form);
            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();
            return new PhotoScenario(form.Id, inspection.Id, asset.Id, locationAttemptId);
        }

        public async Task<InspectionPhotoEvidence?> ReadEvidenceAsync(Guid inspectionId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.InspectionPhotoEvidence.AsNoTracking()
                .SingleOrDefaultAsync(photo => photo.InspectionId == inspectionId);
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
            var databaseName = $"UniPMPhotoRace_{Guid.NewGuid():N}";
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

            await using var compatibilityCommand = connection.CreateCommand();
            compatibilityCommand.CommandText = $"ALTER DATABASE [{databaseName}] SET COMPATIBILITY_LEVEL = 150";
            await compatibilityCommand.ExecuteNonQueryAsync();
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

    private sealed class GatedPhotoStorage(bool releaseImmediately = false) : IInspectionPhotoEvidenceStorage
    {
        private readonly TaskCompletionSource saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseSave = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GatedPhotoStorage()
            : this(releaseImmediately: false)
        {
        }

        private readonly bool saveImmediately = releaseImmediately;

        public Dictionary<string, byte[]> Files { get; } = [];

        public Task WaitForSaveAsync() => saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void ReleaseSave() => releaseSave.TrySetResult();

        public async Task<string> SaveAsync(ReadOnlyMemory<byte> photo, CancellationToken cancellationToken)
        {
            saveStarted.TrySetResult();
            if (!saveImmediately)
            {
                await releaseSave.Task.WaitAsync(cancellationToken);
            }

            var key = $"{Guid.NewGuid():N}.jpg";
            Files[key] = photo.ToArray();
            return key;
        }

        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Files.GetValueOrDefault(storageKey));
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class PhotoInsertGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource insertReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseInsert = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int paused;

        public Task WaitForInsertAsync() => insertReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void ReleaseInsert() => releaseInsert.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PausePhotoInsertAsync(command.CommandText, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PausePhotoInsertAsync(command.CommandText, cancellationToken);
            return result;
        }

        private async Task PausePhotoInsertAsync(string commandText, CancellationToken cancellationToken)
        {
            if (commandText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)
                && commandText.Contains("[InspectionPhotoEvidence]", StringComparison.OrdinalIgnoreCase)
                && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                insertReached.TrySetResult();
                await releaseInsert.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed record PhotoScenario(Guid FormId, Guid InspectionId, Guid AssetId, Guid? LocationAttemptId);
}
