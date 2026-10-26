using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class OperationalInspectionReadScopeEndpointsTests
{
    [Fact]
    public async Task Inspector_reads_only_forms_they_created_owned_or_are_currently_assigned_to()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();
        var fixture = await application.SeedInspectorScenarioAsync();

        var forms = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms/");
        Assert.NotNull(forms);
        Assert.Equal(3, forms.TotalCount);
        Assert.Equal(
            new[] { fixture.SharedFormId, fixture.AssignedFormId, fixture.CreatedDraftFormId }.OrderBy(id => id),
            forms.Items.Select(form => form.Id).OrderBy(id => id));
        var formOptions = await client.GetFromJsonAsync<PreventiveMaintenanceFormOptionsResponse>(
            "/api/v1/preventive-maintenance-forms/options");
        Assert.NotNull(formOptions);
        Assert.Equal(3, formOptions.TotalCount);

        var sharedFormResponse = await client.GetAsync($"/api/v1/preventive-maintenance-forms/{fixture.SharedFormId}");
        Assert.Equal(HttpStatusCode.OK, sharedFormResponse.StatusCode);
        var sharedForm = await sharedFormResponse.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>();
        Assert.NotNull(sharedForm);
        Assert.Equal(2, sharedForm.Inspections.Count);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/preventive-maintenance-forms/{fixture.UnrelatedFormId}")).StatusCode);

        var inspections = await client.GetFromJsonAsync<List<InspectionResponse>>("/api/v1/inspections");
        Assert.NotNull(inspections);
        Assert.Equal(
            new[] { fixture.SharedFirstInspectionId, fixture.SharedSecondInspectionId, fixture.AssignedInspectionId }
                .OrderBy(id => id),
            inspections.Select(inspection => inspection.Id).OrderBy(id => id));
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/inspections/{fixture.SharedSecondInspectionId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/inspections/{fixture.UnrelatedInspectionId}")).StatusCode);

        var photo = await client.GetAsync($"/api/v1/inspections/{fixture.SharedSecondInspectionId}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await photo.Content.ReadAsByteArrayAsync());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/inspections/{fixture.UnrelatedInspectionId}/photo")).StatusCode);
    }

    [Fact]
    public async Task Supervisor_reads_only_forms_on_schedules_currently_assigned_to_them()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Supervisor);
        using var client = application.CreateClient();
        var fixture = await application.SeedSupervisorScenarioAsync();

        var forms = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms/");
        Assert.NotNull(forms);
        Assert.Equal(1, forms.TotalCount);
        Assert.Equal(fixture.AssignedFormId, Assert.Single(forms.Items).Id);
        var formOptions = await client.GetFromJsonAsync<PreventiveMaintenanceFormOptionsResponse>(
            "/api/v1/preventive-maintenance-forms/options");
        Assert.NotNull(formOptions);
        Assert.Equal(1, formOptions.TotalCount);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/inspections/{fixture.AssignedInspectionId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/inspections/{fixture.OtherInspectionId}")).StatusCode);

        var dashboardResponse = await client.GetAsync(
            "/api/v1/pm-period-dashboard?pmCycle=2026-10&assetCategory=fire-extinguisher");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<PmPeriodDashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.Scheduled);
        Assert.Equal(fixture.AssignedScheduleId, Assert.Single(dashboard.Assets).ScheduleId);
    }

    private sealed class TestApplicationFactory(params string[] roles) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-read-scope-{Guid.NewGuid():N}";

        public MemoryPhotoStorage Storage { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IInspectionPhotoEvidenceStorage>();
                services.AddSingleton<IInspectionPhotoEvidenceStorage>(Storage);
            });
        }

        public async Task<InspectorScenario> SeedInspectorScenarioAsync()
        {
            var actorId = TestAuthenticationHandler.UserId;
            var otherId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var actorAsset = NewAsset("SCOPE-ACTOR");
            var sharedAsset = NewAsset("SCOPE-SHARED");
            var assignedAsset = NewAsset("SCOPE-ASSIGNED");
            var unrelatedAsset = NewAsset("SCOPE-OTHER");
            var historicalSchedule = NewSchedule(actorAsset.Id, assignedToUserId: otherId);
            var sharedSchedule = NewSchedule(sharedAsset.Id, assignedToUserId: null);
            var assignedSchedule = NewSchedule(assignedAsset.Id, assignedToUserId: actorId);
            var unrelatedSchedule = NewSchedule(unrelatedAsset.Id, assignedToUserId: otherId);
            var sharedForm = NewForm(otherId, PreventiveMaintenanceFormStatusCatalog.Acknowledged, now);
            var assignedForm = NewForm(otherId, PreventiveMaintenanceFormStatusCatalog.Acknowledged, now);
            var createdDraftForm = NewForm(actorId, PreventiveMaintenanceFormStatusCatalog.Draft, now);
            var unrelatedForm = NewForm(otherId, PreventiveMaintenanceFormStatusCatalog.Acknowledged, now);
            var sharedFirst = NewInspection(actorId, historicalSchedule, actorAsset, sharedForm);
            var sharedSecond = NewInspection(otherId, sharedSchedule, sharedAsset, sharedForm);
            var assignedInspection = NewInspection(otherId, assignedSchedule, assignedAsset, assignedForm);
            var unrelatedInspection = NewInspection(otherId, unrelatedSchedule, unrelatedAsset, unrelatedForm);
            const string photoKey = "shared-batch-photo";
            Storage.Files[photoKey] = [1, 2, 3];

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(actorAsset, sharedAsset, assignedAsset, unrelatedAsset);
            context.PreventiveMaintenanceSchedules.AddRange(
                historicalSchedule, sharedSchedule, assignedSchedule, unrelatedSchedule);
            context.PreventiveMaintenanceForms.AddRange(sharedForm, assignedForm, createdDraftForm, unrelatedForm);
            context.InspectionRecords.AddRange(sharedFirst, sharedSecond, assignedInspection, unrelatedInspection);
            context.InspectionPhotoEvidence.Add(new InspectionPhotoEvidence
            {
                InspectionId = sharedSecond.Id,
                StorageKey = photoKey,
                LengthBytes = 3,
                UploadedAt = now,
                Revision = 1
            });
            await context.SaveChangesAsync();

            return new InspectorScenario(
                sharedForm.Id,
                assignedForm.Id,
                createdDraftForm.Id,
                unrelatedForm.Id,
                sharedFirst.Id,
                sharedSecond.Id,
                assignedInspection.Id,
                unrelatedInspection.Id);
        }

        public async Task<SupervisorScenario> SeedSupervisorScenarioAsync()
        {
            var actorId = TestAuthenticationHandler.UserId;
            var otherId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var assignedAsset = NewAsset("SCOPE-SUPERVISOR");
            var otherAsset = NewAsset("SCOPE-SUPERVISOR-OTHER");
            var assignedSchedule = NewSchedule(assignedAsset.Id, assignedSupervisorUserId: actorId);
            var otherSchedule = NewSchedule(otherAsset.Id, assignedSupervisorUserId: otherId);
            var assignedForm = NewForm(otherId, PreventiveMaintenanceFormStatusCatalog.Acknowledged, now);
            var otherForm = NewForm(otherId, PreventiveMaintenanceFormStatusCatalog.Acknowledged, now);
            var assignedInspection = NewInspection(otherId, assignedSchedule, assignedAsset, assignedForm);
            var otherInspection = NewInspection(otherId, otherSchedule, otherAsset, otherForm);

            await using var context = await CreateContextAsync();
            context.Assets.AddRange(assignedAsset, otherAsset);
            context.PreventiveMaintenanceSchedules.AddRange(assignedSchedule, otherSchedule);
            context.PreventiveMaintenanceForms.AddRange(assignedForm, otherForm);
            context.InspectionRecords.AddRange(assignedInspection, otherInspection);
            await context.SaveChangesAsync();

            return new SupervisorScenario(
                assignedForm.Id,
                assignedSchedule.Id,
                assignedInspection.Id,
                otherInspection.Id);
        }

        private async Task<ApplicationDbContext> CreateContextAsync()
        {
            var factory = Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            return await factory.CreateDbContextAsync();
        }

        private static Asset NewAsset(string code) => new()
        {
            Id = Guid.NewGuid(),
            AssetCode = code,
            AssetCategory = "fire-extinguisher",
            Department = "GSD",
            Status = AssetStatusCatalog.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        private static PreventiveMaintenanceSchedule NewSchedule(
            Guid assetId,
            Guid? assignedToUserId = null,
            Guid? assignedSupervisorUserId = null) => new()
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            ScheduleDate = DateTimeOffset.UtcNow,
            PmCycle = "2026-10",
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Due,
            AssignedToUserId = assignedToUserId,
            AssignedSupervisorUserId = assignedSupervisorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        private static PreventiveMaintenanceForm NewForm(Guid creatorId, string status, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            AssetCategory = "fire-extinguisher",
            Department = "GSD",
            PmCycle = "2026-10",
            PeriodType = "Quarter",
            Status = status,
            CreatedByUserId = creatorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        private static InspectionRecord NewInspection(
            Guid inspectorId,
            PreventiveMaintenanceSchedule schedule,
            Asset asset,
            PreventiveMaintenanceForm form) => new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            AssetId = asset.Id,
            PreventiveMaintenanceFormId = form.Id,
            InspectorUserId = inspectorId,
            DateInspected = DateTimeOffset.UtcNow,
            IsOperational = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class MemoryPhotoStorage : IInspectionPhotoEvidenceStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public Task<string> SaveAsync(ReadOnlyMemory<byte> photo, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Guid.NewGuid().ToString("N");
            Files[key] = photo.ToArray();
            return Task.FromResult(key);
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

    private sealed record InspectorScenario(
        Guid SharedFormId,
        Guid AssignedFormId,
        Guid CreatedDraftFormId,
        Guid UnrelatedFormId,
        Guid SharedFirstInspectionId,
        Guid SharedSecondInspectionId,
        Guid AssignedInspectionId,
        Guid UnrelatedInspectionId);

    private sealed record SupervisorScenario(
        Guid AssignedFormId,
        Guid AssignedScheduleId,
        Guid AssignedInspectionId,
        Guid OtherInspectionId);
}
