using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.AdminManagement;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.AdminManagement;

public sealed class AdminOperationalEndpointsTests
{
    [Fact]
    public async Task Admin_can_create_update_and_delete_assets_individually_in_bulk_and_all_at_once()
    {
        await using var application = new AdminOperationalApplicationFactory();
        using var client = application.CreateClient();

        var first = await CreateAssetAsync(client, "FE-ADMIN-ONE");
        var second = await CreateAssetAsync(client, "FE-ADMIN-TWO");
        var third = await CreateAssetAsync(client, "FE-ADMIN-THREE");

        var updated = await client.PutAsJsonAsync($"/api/v1/admin/assets/{first.Id}", new
        {
            assetCode = "FE-ADMIN-ONE",
            assetCategory = "fire-extinguisher",
            status = "Inactive",
            building = "Test Building",
            department = "GSD",
            location = "North wing"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Inactive", (await updated.Content.ReadFromJsonAsync<AdminAssetRow>())?.Status);

        var listed = await client.GetFromJsonAsync<AdminAssetPageResponse>(
            "/api/v1/admin/assets?search=FE-ADMIN-ONE&page=1&pageSize=25");
        Assert.NotNull(listed);
        Assert.Equal(1, listed.TotalCount);
        Assert.Equal(first.Id, listed.Items[0].Id);

        var individuallyDeleted = await client.DeleteAsync($"/api/v1/admin/assets/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, individuallyDeleted.StatusCode);
        Assert.Equal(1, (await individuallyDeleted.Content.ReadFromJsonAsync<AdminDeleteResult>())?.AssetsRemoved);

        var bulkDeleted = await client.PostAsJsonAsync("/api/v1/admin/assets/bulk-delete", new { ids = new[] { second.Id } });
        Assert.Equal(HttpStatusCode.OK, bulkDeleted.StatusCode);
        Assert.Equal(1, (await bulkDeleted.Content.ReadFromJsonAsync<AdminDeleteResult>())?.AssetsRemoved);

        var allDeleted = await client.DeleteAsync("/api/v1/admin/assets/all");
        Assert.Equal(HttpStatusCode.OK, allDeleted.StatusCode);
        Assert.Equal(1, (await allDeleted.Content.ReadFromJsonAsync<AdminDeleteResult>())?.AssetsRemoved);
        Assert.True(await application.AssetsCountAsync() == 0);
        Assert.NotEqual(Guid.Empty, third.Id);
    }

    [Fact]
    public async Task Admin_schedule_filters_and_form_reversals_preserve_the_documented_lifecycle()
    {
        await using var application = new AdminOperationalApplicationFactory();
        var fixture = await application.SeedMaintenanceAsync();
        using var client = application.CreateClient();

        var dueSchedules = await client.GetFromJsonAsync<AdminSchedulePageResponse>(
            "/api/v1/admin/maintenance/schedules?status=Due&page=1&pageSize=25");
        Assert.NotNull(dueSchedules);
        Assert.Single(dueSchedules.Items);
        Assert.Equal(fixture.DueScheduleId, dueSchedules.Items[0].Id);

        var returned = await client.PostAsync(
            $"/api/v1/admin/maintenance/forms/{fixture.SubmittedFormId}/return-to-draft", null);
        Assert.Equal(HttpStatusCode.NoContent, returned.StatusCode);

        var acknowledgement = await client.DeleteAsync(
            $"/api/v1/admin/maintenance/forms/{fixture.AcknowledgedFormId}/acknowledgement");
        Assert.Equal(HttpStatusCode.OK, acknowledgement.StatusCode);
        Assert.Equal(1, (await acknowledgement.Content.ReadFromJsonAsync<AdminDeleteResult>())?.AcknowledgementsRemoved);
        Assert.Equal("Submitted", await application.FormStatusAsync(fixture.AcknowledgedFormId));

        var cleared = await client.DeleteAsync("/api/v1/admin/demo-data/maintenance");
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var deletion = await cleared.Content.ReadFromJsonAsync<AdminDeleteResult>();
        Assert.NotNull(deletion);
        Assert.Equal(2, deletion.SchedulesRemoved);
        Assert.Equal(2, deletion.FormsRemoved);
        Assert.Equal(0, deletion.AcknowledgementsRemoved);
        Assert.Equal(1, await application.AssetsCountAsync());
        Assert.Equal(0, await application.MaintenanceCountAsync());
    }

    private static async Task<AdminAssetRow> CreateAssetAsync(HttpClient client, string assetCode)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/assets", new
        {
            assetCode,
            assetCategory = "fire-extinguisher",
            building = "Test Building",
            department = "GSD",
            location = "Test area"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminAssetRow>())!;
    }

    private sealed class AdminOperationalApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-admin-operations-{Guid.NewGuid():N}";

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
                services.AddTestAuthentication(AuthRoleCatalog.Admin);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task<MaintenanceFixture> SeedMaintenanceAsync()
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(), AssetCode = $"FE-ADMIN-MAINT-{Guid.NewGuid():N}"[..24],
                AssetCategory = "fire-extinguisher", Building = "Test Building", Department = "GSD",
                Status = "Active", CreatedAt = now, UpdatedAt = now
            };
            var dueSchedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(), AssetId = asset.Id, Asset = asset, ScheduleDate = now,
                PmCycle = "2026-02", PeriodType = "Quarter", Quarter = "Q1", Year = 2026,
                Status = "Due", CreatedAt = now, UpdatedAt = now
            };
            var completedSchedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(), AssetId = asset.Id, Asset = asset, ScheduleDate = now.AddDays(-1),
                PmCycle = "2025-11", PeriodType = "Quarter", Quarter = "Q4", Year = 2025,
                Status = "Completed", CompletedAt = now, CreatedAt = now, UpdatedAt = now
            };
            var submittedForm = CreateForm("Submitted", now);
            var acknowledgedForm = CreateForm("Acknowledged", now);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.AddRange(dueSchedule, completedSchedule);
            context.PreventiveMaintenanceForms.AddRange(submittedForm, acknowledgedForm);
            context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
            {
                Id = Guid.NewGuid(), FormId = acknowledgedForm.Id, Form = acknowledgedForm,
                SignatoryName = "Fictional Department Head", SignatoryPosition = "Department Head",
                CapturedByUserId = TestAuthenticationHandler.UserId, AcknowledgedAt = now
            });
            await context.SaveChangesAsync();
            return new MaintenanceFixture(dueSchedule.Id, submittedForm.Id, acknowledgedForm.Id);
        }

        public async Task<int> AssetsCountAsync()
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.Assets.CountAsync();
        }

        public async Task<string?> FormStatusAsync(Guid formId)
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.PreventiveMaintenanceForms.Where(form => form.Id == formId)
                .Select(form => form.Status).SingleOrDefaultAsync();
        }

        public async Task<int> MaintenanceCountAsync()
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.PreventiveMaintenanceSchedules.CountAsync()
                + await context.PreventiveMaintenanceForms.CountAsync()
                + await context.PreventiveMaintenanceAcknowledgements.CountAsync();
        }

        private static PreventiveMaintenanceForm CreateForm(string status, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(), FileNumber = $"FORM-{Guid.NewGuid():N}"[..16],
            AssetCategory = "fire-extinguisher", Building = "Test Building", Department = "GSD",
            PmCycle = "2026-02", PeriodType = "Quarter", Quarter = "Q1", Year = 2026,
            Status = status, CreatedByUserId = TestAuthenticationHandler.UserId,
            SubmittedByUserId = status == "Draft" ? null : TestAuthenticationHandler.UserId,
            SubmittedAt = status == "Draft" ? null : now, CreatedAt = now, UpdatedAt = now
        };
    }

    private sealed record MaintenanceFixture(Guid DueScheduleId, Guid SubmittedFormId, Guid AcknowledgedFormId);
}
