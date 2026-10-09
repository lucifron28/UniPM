using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class SqlServerInspectionSubmissionIntegrityTests
{
    private const string PreviousMigration = "20260713001356_AddIdentityAuthentication";
    private const string TestPngSignatureBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";

    [SqlServerFact]
    public async Task Migration_preflight_rejects_duplicate_inspections_for_one_schedule()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync(PreviousMigration);

        var now = DateTimeOffset.UtcNow;
        var assetId = Guid.NewGuid();
        var assetCode = $"SQL-{Guid.NewGuid():N}"[..20];
        var assetCategory = "fire-extinguisher";
        var assetStatus = "Active";
        var schedule = new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            ScheduleDate = now,
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Due,
            Quarter = "Q1",
            Year = now.Year,
            CreatedAt = now,
            UpdatedAt = now
        };
        var first = CreateInspection(schedule);
        var second = CreateInspection(schedule);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Assets]
                ([Id], [AssetCode], [AssetCategory], [Status], [CreatedAt], [UpdatedAt])
            VALUES
                ({assetId}, {assetCode}, {assetCategory}, {assetStatus}, {now}, {now});

            INSERT INTO [PreventiveMaintenanceSchedules]
                ([Id], [AssetId], [ScheduleDate], [PeriodType], [Status], [Quarter], [Year], [CreatedAt], [UpdatedAt])
            VALUES
                ({schedule.Id}, {schedule.AssetId}, {schedule.ScheduleDate}, {schedule.PeriodType}, {schedule.Status}, {schedule.Quarter}, {schedule.Year}, {schedule.CreatedAt}, {schedule.UpdatedAt});

            INSERT INTO [InspectionRecords]
                ([Id], [ScheduleId], [AssetId], [InspectorUserId], [DateInspected], [IsOperational], [CreatedAt], [UpdatedAt])
            VALUES
                ({first.Id}, {first.ScheduleId}, {first.AssetId}, {first.InspectorUserId}, {first.DateInspected}, {first.IsOperational}, {first.CreatedAt}, {first.UpdatedAt});

            INSERT INTO [InspectionRecords]
                ([Id], [ScheduleId], [AssetId], [InspectorUserId], [DateInspected], [IsOperational], [CreatedAt], [UpdatedAt])
            VALUES
                ({second.Id}, {second.ScheduleId}, {second.AssetId}, {second.InspectorUserId}, {second.DateInspected}, {second.IsOperational}, {second.CreatedAt}, {second.UpdatedAt});
            """);

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
            var schedule = AddAssetAndSchedule(context, cycleMonth: 8);
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
    public async Task Schedule_unique_index_rejects_duplicate_asset_cycle_rows()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        var first = AddAssetAndSchedule(context, cycleMonth: 2);
        await context.SaveChangesAsync();

        context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = first.AssetId,
            ScheduleDate = first.ScheduleDate,
            PmCycle = first.PmCycle,
            PeriodType = first.PeriodType,
            Quarter = first.Quarter,
            Year = first.Year,
            Status = ScheduleStatusCatalog.Due,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(1, await context.PreventiveMaintenanceSchedules
            .CountAsync(schedule => schedule.AssetId == first.AssetId && schedule.PmCycle == first.PmCycle));
    }

    [SqlServerFact]
    public async Task Concurrent_generation_creates_only_one_schedule_per_asset_cycle()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var year = PreventiveMaintenanceCycle.ToInstitutionalTime(DateTimeOffset.UtcNow).Year;
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = $"GEN-RACE-{Guid.NewGuid():N}"[..20],
            AssetCategory = "fire-extinguisher",
            Building = "Generation Race Test",
            Department = "GEN-RACE-TEST",
            Location = "Test location",
            Status = AssetStatusCatalog.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            UpdatedAt = DateTimeOffset.UtcNow.AddYears(-1)
        };
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
        }

        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/schedules/generate", new { year }),
            client.PostAsJsonAsync("/api/v1/schedules/generate", new { year }));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        await using var verify = database.CreateContext();
        var schedules = await verify.PreventiveMaintenanceSchedules
            .Where(schedule => schedule.AssetId == asset.Id && schedule.PmCycle.StartsWith($"{year:D4}-"))
            .ToListAsync();
        Assert.Equal(4, schedules.Count);
        Assert.Equal(4, schedules.Select(schedule => schedule.PmCycle).Distinct(StringComparer.Ordinal).Count());
    }

    [SqlServerFact]
    public async Task Concurrent_wms_referral_first_writes_return_one_conflict_and_one_audit_row()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var writeGate = new WmsReferralSaveGate();
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate: null,
            wmsReferralWriteGate: writeGate,
            roles: [AuthRoleCatalog.Gsd]);
        var inspectionId = await application.SeedAcknowledgedNonOperationalInspectionAsync();
        writeGate.Arm();
        using var firstClient = application.CreateClient();
        using var secondClient = application.CreateClient();

        var requests = Task.WhenAll(
            firstClient.PutAsJsonAsync(
                $"/api/v1/inspections/{inspectionId}/wms-referral",
                new { externalPmNumber = "WMS-PM-RACE-A", expectedRevision = 0 }),
            secondClient.PutAsJsonAsync(
                $"/api/v1/inspections/{inspectionId}/wms-referral",
                new { externalPmNumber = "WMS-PM-RACE-B", expectedRevision = 0 }));
        try
        {
            await writeGate.WaitForBothSavesAsync();
        }
        finally
        {
            writeGate.ReleaseSaves();
        }

        var responses = await requests;

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await using var context = database.CreateContext();
        var referral = await context.InspectionWmsReferrals.SingleAsync(row => row.InspectionId == inspectionId);
        Assert.Equal(1, referral.Revision);
        Assert.Equal(1, await context.InspectionWmsReferralAudits.CountAsync(row => row.InspectionId == inspectionId));
        Assert.Contains(referral.ExternalPmNumber, new[] { "WMS-PM-RACE-A", "WMS-PM-RACE-B" });
    }

    [SqlServerFact]
    public async Task Concurrent_wms_referral_corrections_return_one_conflict_without_extra_audit_history()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        var writeGate = new WmsReferralSaveGate();
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate: null,
            wmsReferralWriteGate: writeGate,
            roles: [AuthRoleCatalog.Gsd]);
        var inspectionId = await application.SeedAcknowledgedNonOperationalInspectionAsync();
        using var firstClient = application.CreateClient();
        using var secondClient = application.CreateClient();
        var initial = await firstClient.PutAsJsonAsync(
            $"/api/v1/inspections/{inspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-RACE-INITIAL", expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        writeGate.Arm();

        var requests = Task.WhenAll(
            firstClient.PutAsJsonAsync(
                $"/api/v1/inspections/{inspectionId}/wms-referral",
                new { externalPmNumber = "WMS-PM-RACE-CORRECTION-A", expectedRevision = 1 }),
            secondClient.PutAsJsonAsync(
                $"/api/v1/inspections/{inspectionId}/wms-referral",
                new { externalPmNumber = "WMS-PM-RACE-CORRECTION-B", expectedRevision = 1 }));
        try
        {
            await writeGate.WaitForBothSavesAsync();
        }
        finally
        {
            writeGate.ReleaseSaves();
        }

        var responses = await requests;

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await using var context = database.CreateContext();
        var referral = await context.InspectionWmsReferrals.SingleAsync(row => row.InspectionId == inspectionId);
        Assert.Equal(2, referral.Revision);
        Assert.Equal(2, await context.InspectionWmsReferralAudits.CountAsync(row => row.InspectionId == inspectionId));
        Assert.Contains(referral.ExternalPmNumber, new[] { "WMS-PM-RACE-CORRECTION-A", "WMS-PM-RACE-CORRECTION-B" });
    }

    [SqlServerFact]
    public async Task Concurrent_form_submissions_assign_distinct_provisional_file_numbers()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var formIds = await application.SeedDraftFormsAsync();
        using var firstClient = application.CreateClient();
        using var secondClient = application.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsync($"/api/v1/preventive-maintenance-forms/{formIds[0]}/submit", content: null),
            secondClient.PostAsync($"/api/v1/preventive-maintenance-forms/{formIds[1]}/submit", content: null));

        var failedResponses = await Task.WhenAll(responses
            .Where(response => !response.IsSuccessStatusCode)
            .Select(async response =>
                $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));
        Assert.True(failedResponses.Length == 0, string.Join(Environment.NewLine, failedResponses));
        var submitted = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>()));
        Assert.All(submitted, form => Assert.NotNull(form));
        var fileNumbers = submitted.Select(form => form!.FileNumber!).ToArray();
        Assert.All(fileNumbers, fileNumber => Assert.Matches("^PMF-[0-9]{4}-[0-9]{4}$", fileNumber));
        Assert.Equal(2, fileNumbers.Distinct(StringComparer.Ordinal).Count());

        await using var context = database.CreateContext();
        var storedForms = await context.PreventiveMaintenanceForms
            .Where(form => formIds.Contains(form.Id))
            .ToListAsync();
        Assert.All(storedForms, form =>
        {
            Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Submitted, form.Status);
            Assert.NotNull(form.FileNumber);
        });
    }

    [SqlServerFact]
    public async Task Supervisor_reassignment_wins_lock_before_inspection_start_and_inspector_is_reauthorized()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var seedApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var scenario = await seedApplication.SeedBatchMutationScenarioAsync();
        var mutationGate = new ScheduleMutationCommandGate();

        await using var gsdApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate,
            AuthRoleCatalog.Gsd);
        await using var inspectorApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Inspector);
        using var gsdClient = gsdApplication.CreateClient();
        using var inspectorClient = inspectorApplication.CreateClient();

        var assignmentTask = gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{scenario.ScheduleId}/supervisor-assignment",
            new { supervisorUserId = scenario.NewSupervisorId });
        await mutationGate.WaitForScheduleUpdateAsync();
        var inspectionTask = inspectorClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{scenario.FormId}/inspections",
            NewInspectionRequest(scenario.ScheduleId, TestAuthenticationHandler.UserId));
        try
        {
            await WaitForApplicationLockWaitAsync(
                database.ConnectionString,
                expectedWaiters: 1,
                competingMutation: inspectionTask);
        }
        finally
        {
            mutationGate.ReleaseScheduleUpdate();
        }

        using var assignmentResponse = await assignmentTask;
        using var inspectionResponse = await inspectionTask;
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, inspectionResponse.StatusCode);

        await using var verificationContext = database.CreateContext();
        var schedule = await verificationContext.PreventiveMaintenanceSchedules.SingleAsync(
            candidate => candidate.Id == scenario.ScheduleId);
        Assert.Equal(scenario.NewSupervisorId, schedule.AssignedSupervisorUserId);
        Assert.Null(schedule.AssignedToUserId);
        Assert.Equal(ScheduleStatusCatalog.Due, schedule.Status);
        Assert.Empty(await verificationContext.InspectionRecords
            .Where(inspection => inspection.ScheduleId == scenario.ScheduleId)
            .ToListAsync());
    }

    [SqlServerFact]
    public async Task Inspection_start_wins_lock_before_supervisor_reassignment_and_assignment_is_rejected()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var seedApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var scenario = await seedApplication.SeedBatchMutationScenarioAsync();
        var mutationGate = new ScheduleMutationCommandGate();

        await using var gsdApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        await using var inspectorApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate,
            AuthRoleCatalog.Inspector);
        using var gsdClient = gsdApplication.CreateClient();
        using var inspectorClient = inspectorApplication.CreateClient();

        var inspectionTask = inspectorClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{scenario.FormId}/inspections",
            NewInspectionRequest(scenario.ScheduleId, TestAuthenticationHandler.UserId));
        await mutationGate.WaitForScheduleUpdateAsync();
        var assignmentTask = gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{scenario.ScheduleId}/supervisor-assignment",
            new { supervisorUserId = scenario.NewSupervisorId });
        try
        {
            await WaitForApplicationLockWaitAsync(
                database.ConnectionString,
                expectedWaiters: 1,
                competingMutation: assignmentTask);
        }
        finally
        {
            mutationGate.ReleaseScheduleUpdate();
        }

        using var inspectionResponse = await inspectionTask;
        using var assignmentResponse = await assignmentTask;
        Assert.Equal(HttpStatusCode.Created, inspectionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, assignmentResponse.StatusCode);

        await using var verificationContext = database.CreateContext();
        var schedule = await verificationContext.PreventiveMaintenanceSchedules.SingleAsync(
            candidate => candidate.Id == scenario.ScheduleId);
        Assert.Equal(scenario.ExistingSupervisorId, schedule.AssignedSupervisorUserId);
        Assert.Equal(TestAuthenticationHandler.UserId, schedule.AssignedToUserId);
        Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
        Assert.NotNull(schedule.CompletedAt);
        Assert.Single(await verificationContext.InspectionRecords
            .Where(inspection => inspection.ScheduleId == scenario.ScheduleId)
            .ToListAsync());
    }

    [SqlServerFact]
    public async Task Registration_and_recovery_wait_for_assignment_then_defer_locked_cycle()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var seedApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var institutionalYear = PreventiveMaintenanceCycle.ToInstitutionalTime(DateTimeOffset.UtcNow).Year;
        var scenario = await seedApplication.SeedBatchMutationScenarioAsync(
            $"{institutionalYear:D4}-12",
            "fire-alarm");
        var mutationGate = new ScheduleMutationCommandGate();

        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var assignmentTask = client.PutAsJsonAsync(
            $"/api/v1/schedules/{scenario.ScheduleId}/supervisor-assignment",
            new { supervisorUserId = scenario.NewSupervisorId });
        await mutationGate.WaitForScheduleUpdateAsync();

        var registrationTask = client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = $"RACE-NEW-{Guid.NewGuid():N}"[..20],
            assetCategory = "fire-alarm",
            department = "RACE-TEST",
            building = "Race Test Building",
            location = "Ground Floor"
        });
        var generationTask = client.PostAsJsonAsync(
            "/api/v1/schedules/generate",
            new { year = institutionalYear });
        try
        {
            await WaitForApplicationLockWaitAsync(
                database.ConnectionString,
                expectedWaiters: 2,
                competingMutation: registrationTask);
        }
        finally
        {
            mutationGate.ReleaseScheduleUpdate();
        }

        using var assignmentResponse = await assignmentTask;
        using var registrationResponse = await registrationTask;
        using var generationResponse = await generationTask;
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, generationResponse.StatusCode);

        using var assetDocument = JsonDocument.Parse(await registrationResponse.Content.ReadAsStringAsync());
        var newAssetId = assetDocument.RootElement.GetProperty("id").GetGuid();
        await using var context = database.CreateContext();
        var newAssetCycles = await context.PreventiveMaintenanceSchedules
            .Where(schedule => schedule.AssetId == newAssetId)
            .Select(schedule => schedule.PmCycle)
            .ToListAsync();
        Assert.DoesNotContain($"{institutionalYear:D4}-12", newAssetCycles);
        var deferral = await context.ScheduleEnrollmentDeferrals
            .SingleAsync(item => item.AssetId == newAssetId
                && item.PmCycle == $"{institutionalYear:D4}-12");
        Assert.Equal("BatchAssigned", deferral.ReasonCode);
        var existingSchedule = await context.PreventiveMaintenanceSchedules
            .SingleAsync(schedule => schedule.Id == scenario.ScheduleId);
        Assert.Equal(scenario.NewSupervisorId, existingSchedule.AssignedSupervisorUserId);
        Assert.Null(existingSchedule.AssignedToUserId);
    }

    [SqlServerFact]
    public async Task Manual_schedule_creation_commits_the_schedule_with_its_batch_lock()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var institutionalYear = PreventiveMaintenanceCycle.ToInstitutionalTime(DateTimeOffset.UtcNow).Year;
        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = $"MANUAL-CREATE-{Guid.NewGuid():N}"[..20],
            AssetCategory = "fire-alarm",
            Building = "Manual Schedule Test",
            Department = "MANUAL-SCHEDULE-TEST",
            Location = "Ground Floor",
            Status = AssetStatusCatalog.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await using (var seedContext = database.CreateContext())
        {
            seedContext.Assets.Add(asset);
            await seedContext.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = $"{institutionalYear:D4}-12",
            periodType = "Semester"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verificationContext = database.CreateContext();
        Assert.True(await verificationContext.PreventiveMaintenanceSchedules
            .AnyAsync(schedule => schedule.AssetId == asset.Id
                && schedule.PmCycle == $"{institutionalYear:D4}-12"));
    }

    [SqlServerFact]
    public async Task Manual_schedule_creation_waits_for_assignment_and_rejects_locked_batch()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var seedApplication = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var institutionalYear = PreventiveMaintenanceCycle.ToInstitutionalTime(DateTimeOffset.UtcNow).Year;
        var pmCycle = $"{institutionalYear:D4}-12";
        var scenario = await seedApplication.SeedBatchMutationScenarioAsync(pmCycle, "fire-alarm");
        var lateAsset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = $"RACE-MANUAL-{Guid.NewGuid():N}"[..20],
            AssetCategory = "fire-alarm",
            Building = "Race Test Building",
            Department = "RACE-TEST",
            Location = "Ground Floor",
            Status = AssetStatusCatalog.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await using (var seedContext = database.CreateContext())
        {
            seedContext.Assets.Add(lateAsset);
            await seedContext.SaveChangesAsync();
        }

        var mutationGate = new ScheduleMutationCommandGate();
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            mutationGate,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var assignmentTask = client.PutAsJsonAsync(
            $"/api/v1/schedules/{scenario.ScheduleId}/supervisor-assignment",
            new { supervisorUserId = scenario.NewSupervisorId });
        await mutationGate.WaitForScheduleUpdateAsync();
        var scheduleCreationTask = client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = lateAsset.Id,
            pmCycle,
            periodType = "Semester"
        });
        try
        {
            await WaitForApplicationLockWaitAsync(
                database.ConnectionString,
                expectedWaiters: 1,
                competingMutation: scheduleCreationTask);
        }
        finally
        {
            mutationGate.ReleaseScheduleUpdate();
        }

        using var assignmentResponse = await assignmentTask;
        using var scheduleCreationResponse = await scheduleCreationTask;
        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, scheduleCreationResponse.StatusCode);

        await using var verificationContext = database.CreateContext();
        Assert.False(await verificationContext.PreventiveMaintenanceSchedules
            .AnyAsync(schedule => schedule.AssetId == lateAsset.Id && schedule.PmCycle == pmCycle));
        var assignedSchedule = await verificationContext.PreventiveMaintenanceSchedules
            .SingleAsync(schedule => schedule.Id == scenario.ScheduleId);
        Assert.Equal(scenario.NewSupervisorId, assignedSchedule.AssignedSupervisorUserId);
    }

    private static object NewInspectionRequest(Guid scheduleId, Guid inspectorUserId)
    {
        return new
        {
            scheduleId,
            inspectorUserId,
            dateInspected = DateTimeOffset.UtcNow,
            isOperational = true,
            remarks = "Serialized schedule mutation test"
        };
    }

    private static async Task WaitForApplicationLockWaitAsync(
        string connectionString,
        int expectedWaiters,
        Task<HttpResponseMessage> competingMutation)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < timeout)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(DISTINCT locks.request_session_id)
                FROM sys.dm_tran_locks AS locks
                INNER JOIN sys.dm_exec_requests AS requests
                    ON requests.session_id = locks.request_session_id
                WHERE locks.resource_type = N'APPLICATION'
                    AND locks.resource_database_id = DB_ID()
                    AND locks.request_mode = N'X'
                    AND locks.request_status = N'WAIT'
                    AND requests.wait_type LIKE N'LCK_M_%';
                """;
            var waiters = Convert.ToInt32(
                await command.ExecuteScalarAsync(),
                System.Globalization.CultureInfo.InvariantCulture);
            if (waiters >= expectedWaiters)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        var mutationState = competingMutation.IsCompletedSuccessfully
            ? $"HTTP {(int)competingMutation.Result.StatusCode}"
            : competingMutation.Status.ToString();
        var lockState = await GetApplicationLockStateAsync(connectionString);
        throw new TimeoutException(
            $"Expected {expectedWaiters} HTTP mutations waiting for the SQL Server schedule-batch application lock; competing mutation={mutationState}; application locks={lockState}.");
    }

    private static async Task<string> GetApplicationLockStateAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(locks.request_status, N'none'),
                COALESCE(locks.request_mode, N'none'),
                COALESCE(requests.wait_type, N'none'),
                COUNT(*)
            FROM sys.dm_tran_locks AS locks
            LEFT JOIN sys.dm_exec_requests AS requests
                ON requests.session_id = locks.request_session_id
            WHERE locks.resource_type = N'APPLICATION'
                AND locks.resource_database_id = DB_ID()
            GROUP BY locks.request_status, locks.request_mode, requests.wait_type;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var states = new List<string>();
        while (await reader.ReadAsync())
        {
            states.Add($"{reader.GetString(0)}/{reader.GetString(1)}/{reader.GetString(2)}={reader.GetInt32(3)}");
        }

        return states.Count == 0 ? "none-visible" : string.Join(',', states);
    }

    [SqlServerFact]
    public async Task Acknowledging_submitted_form_preserves_completed_schedule_status_and_completion_times()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync(RequireSqlServerConnection());
        await using var application = new SqlServerInspectionApplicationFactory(
            database.ConnectionString,
            AuthRoleCatalog.Gsd);
        var formId = await application.SeedSubmittedFormAsync();
        var completedAtBeforeAcknowledgement = new Dictionary<Guid, DateTimeOffset?>();
        await using (var beforeContext = database.CreateContext())
        {
            var beforeScheduleIds = await beforeContext.InspectionRecords
                .Where(record => record.PreventiveMaintenanceFormId == formId)
                .Select(record => record.ScheduleId)
                .ToListAsync();
            var schedulesBeforeAcknowledgement = await beforeContext.PreventiveMaintenanceSchedules
                .Where(schedule => beforeScheduleIds.Contains(schedule.Id))
                .ToListAsync();
            Assert.Equal(2, schedulesBeforeAcknowledgement.Count);
            Assert.All(schedulesBeforeAcknowledgement, schedule =>
            {
                Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
                Assert.NotNull(schedule.CompletedAt);
            });
            foreach (var schedule in schedulesBeforeAcknowledgement)
            {
                completedAtBeforeAcknowledgement.Add(schedule.Id, schedule.CompletedAt);
            }
        }

        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{formId}/acknowledge",
            new
            {
                signatoryName = "Fictional Department Head",
                signatoryPosition = "Department Head",
                signatureData = TestPngSignatureBase64,
                signatureContentType = "image/png"
            });

        response.EnsureSuccessStatusCode();

        await using var context = database.CreateContext();
        var form = await context.PreventiveMaintenanceForms
            .Include(candidate => candidate.Acknowledgement)
            .SingleAsync(candidate => candidate.Id == formId);
        var inspectionIds = await context.InspectionRecords
            .Where(record => record.PreventiveMaintenanceFormId == formId)
            .Select(record => record.Id)
            .ToListAsync();
        var scheduleIds = await context.InspectionRecords
            .Where(record => record.PreventiveMaintenanceFormId == formId)
            .Select(record => record.ScheduleId)
            .ToListAsync();
        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => scheduleIds.Contains(schedule.Id))
            .ToListAsync();
        Assert.NotNull(form.Acknowledgement);
        Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Acknowledged, form.Status);
        Assert.Equal(2, inspectionIds.Count);
        Assert.Equal(2, schedules.Count);
        Assert.All(schedules, schedule =>
        {
            Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
            Assert.Equal(completedAtBeforeAcknowledgement[schedule.Id], schedule.CompletedAt);
        });
    }

    private static PreventiveMaintenanceSchedule AddAssetAndSchedule(
        ApplicationDbContext context,
        int cycleMonth = 2,
        bool completed = false)
    {
        var now = DateTimeOffset.UtcNow;
        var year = now.Year;
        var pmCycle = $"{year:D4}-{cycleMonth:D2}";
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
            ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
            PmCycle = pmCycle,
            PeriodType = "Quarter",
            Status = completed ? ScheduleStatusCatalog.Completed : ScheduleStatusCatalog.Due,
            Quarter = $"Q{((cycleMonth - 1) / 3) + 1}",
            Year = year,
            CompletedAt = completed ? now : null,
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

    private sealed class SqlServerInspectionApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;
        private readonly string[] roles;
        private readonly ScheduleMutationCommandGate? mutationGate;
        private readonly WmsReferralSaveGate? wmsReferralWriteGate;

        public SqlServerInspectionApplicationFactory(string connectionString, params string[] roles)
            : this(connectionString, null, null, roles)
        {
        }

        public SqlServerInspectionApplicationFactory(
            string connectionString,
            ScheduleMutationCommandGate? mutationGate,
            params string[] roles)
            : this(connectionString, mutationGate, null, roles)
        {
        }

        public SqlServerInspectionApplicationFactory(
            string connectionString,
            ScheduleMutationCommandGate? mutationGate,
            WmsReferralSaveGate? wmsReferralWriteGate,
            params string[] roles)
        {
            this.connectionString = connectionString;
            this.mutationGate = mutationGate;
            this.wmsReferralWriteGate = wmsReferralWriteGate;
            this.roles = roles.Length == 0 ? [AuthRoleCatalog.Inspector] : roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                {
                    options.UseUniPmSqlServer(connectionString);
                    if (mutationGate is not null)
                    {
                        options.AddInterceptors(mutationGate);
                    }
                    if (wmsReferralWriteGate is not null)
                    {
                        options.AddInterceptors(wmsReferralWriteGate);
                    }
                });
            });
        }

        public async Task<Guid[]> SeedDraftFormsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "sql-form-submitter@unipm.local",
                NormalizedUserName = "SQL-FORM-SUBMITTER@UNIPM.LOCAL",
                Email = "sql-form-submitter@unipm.local",
                NormalizedEmail = "SQL-FORM-SUBMITTER@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "SQL Form Submitter",
                IsActive = true
            });
            var firstSchedule = AddAssetAndSchedule(context, cycleMonth: 2, completed: true);
            var secondSchedule = AddAssetAndSchedule(context, cycleMonth: 5, completed: true);
            var now = DateTimeOffset.UtcNow;
            var forms = new[]
            {
                CreateDraftForm(firstSchedule, now),
                CreateDraftForm(secondSchedule, now)
            };

            context.PreventiveMaintenanceForms.AddRange(forms);
            await context.SaveChangesAsync();
            return forms.Select(form => form.Id).ToArray();
        }

        public async Task<BatchMutationScenario> SeedBatchMutationScenarioAsync(
            string pmCycle = "2026-08",
            string assetCategory = "fire-extinguisher")
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in new[] { AuthRoleCatalog.Inspector, AuthRoleCatalog.Supervisor })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
                }
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var actor = new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = $"sql-batch-inspector-{Guid.NewGuid():N}@unipm.local",
                Email = $"sql-batch-inspector-{Guid.NewGuid():N}@unipm.local",
                EmailConfirmed = true,
                DisplayName = "SQL Batch Inspector",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(actor, "Demo-Account-123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(actor, AuthRoleCatalog.Inspector)).Succeeded);

            var existingSupervisor = await CreateActiveUserAsync(userManager, "existing-supervisor", AuthRoleCatalog.Supervisor);
            var newSupervisor = await CreateActiveUserAsync(userManager, "new-supervisor", AuthRoleCatalog.Supervisor);
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"RACE-{Guid.NewGuid():N}"[..20],
                AssetCategory = assetCategory,
                Building = "Race Test Building",
                Department = "RACE-TEST",
                Location = "Ground Floor",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
            var periodType = CpmpScheduleFrequency.GetMonths(assetCategory).Count == 4
                ? SchedulePeriodTypeCatalog.Quarter
                : SchedulePeriodTypeCatalog.Semester;
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Asset = asset,
                ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle),
                PmCycle = pmCycle,
                PeriodType = periodType,
                Quarter = periodType == SchedulePeriodTypeCatalog.Quarter
                    ? $"Q{((month - 1) / 3) + 1}"
                    : null,
                Year = year,
                Status = ScheduleStatusCatalog.Due,
                AssignedToUserId = actor.Id,
                AssignedSupervisorUserId = existingSupervisor.Id,
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
                CreatedByUserId = actor.Id,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.PreventiveMaintenanceForms.Add(form);
            await context.SaveChangesAsync();
            return new BatchMutationScenario(schedule.Id, asset.Id, form.Id, existingSupervisor.Id, newSupervisor.Id);
        }

        private static async Task<ApplicationUser> CreateActiveUserAsync(
            UserManager<ApplicationUser> userManager,
            string suffix,
            string role)
        {
            var email = $"sql-{suffix}-{Guid.NewGuid():N}@unipm.local";
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = suffix,
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(user, "Demo-Account-123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }

        public async Task<Guid> SeedSubmittedFormAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();
            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "sql-form-acknowledger@unipm.local",
                NormalizedUserName = "SQL-FORM-ACKNOWLEDGER@UNIPM.LOCAL",
                Email = "sql-form-acknowledger@unipm.local",
                NormalizedEmail = "SQL-FORM-ACKNOWLEDGER@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "SQL Form Acknowledger",
                IsActive = true
            });
            var firstSchedule = AddAssetAndSchedule(context, cycleMonth: 11, completed: true);
            var secondSchedule = AddAssetAndSchedule(context, cycleMonth: 11, completed: true);
            var now = DateTimeOffset.UtcNow;
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = "fire-extinguisher",
                Building = "Test Building",
                Department = "GSD",
                PmCycle = firstSchedule.PmCycle,
                PeriodType = "Quarter",
                Quarter = firstSchedule.Quarter,
                Year = firstSchedule.Year,
                Status = PreventiveMaintenanceFormStatusCatalog.Submitted,
                CreatedByUserId = TestAuthenticationHandler.UserId,
                SubmittedByUserId = TestAuthenticationHandler.UserId,
                SubmittedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PreventiveMaintenanceForms.Add(form);
            context.InspectionRecords.AddRange(
                CreateFormInspection(form, firstSchedule, now),
                CreateFormInspection(form, secondSchedule, now));
            await context.SaveChangesAsync();
            return form.Id;
        }

        public async Task<Guid> SeedAcknowledgedNonOperationalInspectionAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();

            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"WMS-RACE-{Guid.NewGuid():N}"[..20],
                AssetCategory = "fire-extinguisher",
                Building = "WMS Race Test",
                Department = "GSD",
                Location = "Test Location",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = PreventiveMaintenanceCycle.DeadlineForCycle("2026-08"),
                PmCycle = "2026-08",
                PeriodType = "Quarter",
                Quarter = "Q3",
                Year = 2026,
                Status = ScheduleStatusCatalog.Completed,
                CompletedAt = now.AddMinutes(-1),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
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
                Status = PreventiveMaintenanceFormStatusCatalog.Acknowledged,
                CreatedByUserId = TestAuthenticationHandler.UserId,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
            };
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                AssetId = asset.Id,
                PreventiveMaintenanceFormId = form.Id,
                InspectorUserId = TestAuthenticationHandler.UserId,
                DateInspected = now.AddDays(-1),
                CompletedAt = now.AddMinutes(-1),
                IsOperational = false,
                Remarks = "WMS concurrent correction test",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
            };
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.PreventiveMaintenanceForms.Add(form);
            context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                SignatoryName = "Demo Signatory",
                SignatoryPosition = "Department Head",
                CapturedByUserId = TestAuthenticationHandler.UserId,
                AcknowledgedAt = now.AddMinutes(-1)
            });
            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();
            return inspection.Id;
        }
    }

    private sealed class ScheduleMutationCommandGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource commandReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseCommand = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int paused;

        public async Task WaitForScheduleUpdateAsync()
        {
            await commandReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void ReleaseScheduleUpdate() => releaseCommand.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PauseBeforeScheduleUpdateAsync(command.CommandText, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PauseBeforeScheduleUpdateAsync(command.CommandText, cancellationToken);
            return result;
        }

        private async Task PauseBeforeScheduleUpdateAsync(string commandText, CancellationToken cancellationToken)
        {
            if (Regex.IsMatch(
                    commandText,
                    @"\bUPDATE\s+(?:\[dbo\]\.)?\[PreventiveMaintenanceSchedules\]",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                && Interlocked.CompareExchange(ref paused, 1, 0) == 0)
            {
                commandReached.TrySetResult();
                await releaseCommand.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class WmsReferralSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothSavesReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseSaves = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        private int arrived;

        public void Arm() => Interlocked.Exchange(ref armed, 1);

        public Task WaitForBothSavesAsync() => bothSavesReached.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public void ReleaseSaves() => releaseSaves.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 0)
            {
                return result;
            }

            if (Interlocked.Increment(ref arrived) == 2)
            {
                bothSavesReached.TrySetResult();
            }

            await Task.WhenAny(bothSavesReached.Task, releaseSaves.Task)
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            await releaseSaves.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    private sealed record BatchMutationScenario(
        Guid ScheduleId,
        Guid AssetId,
        Guid FormId,
        Guid ExistingSupervisorId,
        Guid NewSupervisorId);

    private static InspectionRecord CreateFormInspection(
        PreventiveMaintenanceForm form,
        PreventiveMaintenanceSchedule schedule,
        DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            PreventiveMaintenanceFormId = form.Id,
            AssetId = schedule.AssetId,
            InspectorUserId = TestAuthenticationHandler.UserId,
            DateInspected = now,
            CompletedAt = now,
            IsOperational = true,
            Remarks = "Native SQL acknowledgement test row",
            CreatedAt = now,
            UpdatedAt = now
        };

    private static PreventiveMaintenanceForm CreateDraftForm(
        PreventiveMaintenanceSchedule schedule,
        DateTimeOffset now)
    {
        var form = new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            AssetCategory = "fire-extinguisher",
            Building = "Test Building",
            Department = "GSD",
            PmCycle = schedule.PmCycle,
            PeriodType = "Quarter",
            Quarter = schedule.Quarter,
            Year = schedule.Year,
            Status = PreventiveMaintenanceFormStatusCatalog.Draft,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        form.Inspections.Add(new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            PreventiveMaintenanceFormId = form.Id,
            AssetId = schedule.AssetId,
            InspectorUserId = TestAuthenticationHandler.UserId,
            DateInspected = now,
            CompletedAt = now,
            IsOperational = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        return form;
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
}
