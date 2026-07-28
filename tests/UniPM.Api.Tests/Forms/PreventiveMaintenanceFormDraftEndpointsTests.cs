using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PreventiveMaintenanceFormDraftEndpointsTests
{
    [Fact]
    public async Task Create_draft_form_can_contain_multiple_inspection_rows()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-001", "fire-extinguisher");
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 2);

        var form = await CreateFormAsync(client, asset.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, "First draft row");
        await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second draft row");

        var response = await client.GetAsync($"/api/v1/preventive-maintenance-forms/{form.Id}");

        response.EnsureSuccessStatusCode();
        var persisted = await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>();
        Assert.NotNull(persisted);
        Assert.Equal("Draft", persisted.Status);
        Assert.Equal(2, persisted.Inspections.Count);
        Assert.Equal([firstSchedule.Id, secondSchedule.Id], persisted.Inspections.Select(row => row.ScheduleId).ToArray());
    }

    [Fact]
    public async Task Draft_rows_reject_duplicate_or_category_mismatched_schedules()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var fireExtinguisher = await CreateAssetAsync(client, "FE-FORM-002", "fire-extinguisher");
        var fireAlarm = await CreateAssetAsync(client, "FA-FORM-001", "fire-alarm");
        var fireExtinguisherSchedule = await CreateScheduleAsync(client, fireExtinguisher.Id, 1);
        var fireAlarmSchedule = await CreateScheduleAsync(client, fireAlarm.Id, 1);
        var form = await CreateFormAsync(client, fireExtinguisher.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, fireExtinguisherSchedule.Id, "Draft pressure check");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireExtinguisherSchedule.Id, "Duplicate"));
        var categoryMismatch = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireAlarmSchedule.Id, "Wrong category"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categoryMismatch.StatusCode);
    }

    [Theory]
    [InlineData(PreventiveMaintenanceFormStatusCatalog.Submitted)]
    [InlineData(PreventiveMaintenanceFormStatusCatalog.Acknowledged)]
    public async Task Submitted_or_acknowledged_forms_are_immutable(string status)
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, $"FE-FORM-{status}", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "Original draft row");
        await application.SetFormStatusAsync(form.Id, status);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{row.Id}",
            DraftInspectionRequest(schedule.Id, "Updated draft row"));
        var delete = await client.DeleteAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{row.Id}");

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Draft_rows_are_excluded_from_official_history_and_search_projection()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-003", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "mahina ang pressure");

        var history = await client.GetAsync($"/api/v1/inspections/history/{asset.Id}");
        var list = await client.GetAsync("/api/v1/inspections");
        var detail = await client.GetAsync($"/api/v1/inspections/{row.Id}");
        await using var scope = application.Services.CreateAsyncScope();
        var projector = scope.ServiceProvider.GetRequiredService<MaintenanceSearchDocumentProjector>();
        var rebuild = await projector.RebuildAsync();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        Assert.Empty(await history.Content.ReadFromJsonAsync<List<InspectionHistoryResponse>>() ?? []);
        Assert.Empty(await list.Content.ReadFromJsonAsync<List<InspectionResponse>>() ?? []);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(0, rebuild.Total);
        Assert.Empty(await context.MaintenanceSearchDocuments.ToListAsync());
    }

    private static async Task<AssetResponse> CreateAssetAsync(HttpClient client, string assetCode, string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory,
            building = "Main Building",
            department = "GSD",
            location = "Test Area"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetResponse>())!;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(HttpClient client, Guid assetId, int month)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = new DateTimeOffset(2026, month, 10, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduleResponse>())!;
    }

    private static async Task<PreventiveMaintenanceFormResponse> CreateFormAsync(HttpClient client, string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/preventive-maintenance-forms/", new
        {
            assetCategory,
            building = "Main Building",
            department = "GSD",
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>())!;
    }

    private static async Task<DraftInspectionRowResponse> AddInspectionRowAsync(
        HttpClient client,
        Guid formId,
        Guid scheduleId,
        string remarks)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{formId}/inspections",
            DraftInspectionRequest(scheduleId, remarks));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DraftInspectionRowResponse>())!;
    }

    private static object DraftInspectionRequest(Guid scheduleId, string remarks)
    {
        return new
        {
            scheduleId,
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            isOperational = false,
            remarks,
            actionsRecommendations = "Inspect during final submission."
        };
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-form-drafts-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(AuthRoleCatalog.Gsd);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task EnsureAuthenticatedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            if (await context.Users.AnyAsync(user => user.Id == TestAuthenticationHandler.UserId))
            {
                return;
            }

            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "form-drafts@unipm.local",
                NormalizedUserName = "FORM-DRAFTS@UNIPM.LOCAL",
                Email = "form-drafts@unipm.local",
                NormalizedEmail = "FORM-DRAFTS@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "Form Drafts User",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        public async Task SetFormStatusAsync(Guid formId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var form = await context.PreventiveMaintenanceForms.SingleAsync(candidate => candidate.Id == formId);
            form.Status = status;
            await context.SaveChangesAsync();
        }
    }

    private sealed record AssetResponse(Guid Id, string AssetCategory);

    private sealed record ScheduleResponse(Guid Id);
}
