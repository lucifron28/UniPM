using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class InspectionQueryEndpointsTests
{
    [Fact]
    public async Task List_inspections_returns_all_inspections()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);

        var response = await client.GetAsync("/api/v1/inspections");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        Assert.Equal(3, inspections.Count);
        Assert.Equal(scenario.ThirdInspection.Id, inspections[0].Id);
    }

    [Fact]
    public async Task List_inspections_filters_by_asset_id()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);

        var response = await client.GetAsync($"/api/v1/inspections?assetId={scenario.FirstAsset.Id}");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        Assert.Equal(2, inspections.Count);
        Assert.All(inspections, inspection => Assert.Equal(scenario.FirstAsset.Id, inspection.AssetId));
    }

    [Fact]
    public async Task List_inspections_filters_by_schedule_id()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);

        var response = await client.GetAsync($"/api/v1/inspections?scheduleId={scenario.SecondSchedule.Id}");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        var inspection = Assert.Single(inspections);
        Assert.Equal(scenario.SecondSchedule.Id, inspection.ScheduleId);
    }

    [Fact]
    public async Task List_inspections_filters_by_operational_status()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        await CreateScenarioAsync(application, client);

        var response = await client.GetAsync("/api/v1/inspections?isOperational=false");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        var inspection = Assert.Single(inspections);
        Assert.False(inspection.IsOperational);
    }

    [Fact]
    public async Task List_inspections_filters_from_date()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);
        var dateFrom = Uri.EscapeDataString("2026-02-01T00:00:00+08:00");

        var response = await client.GetAsync($"/api/v1/inspections?dateFrom={dateFrom}");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        Assert.Equal(2, inspections.Count);
        Assert.DoesNotContain(inspections, inspection => inspection.Id == scenario.FirstInspection.Id);
    }

    [Fact]
    public async Task List_inspections_filters_to_date()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);
        var dateTo = Uri.EscapeDataString("2026-02-28T23:59:59+08:00");

        var response = await client.GetAsync($"/api/v1/inspections?dateTo={dateTo}");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        Assert.Equal(2, inspections.Count);
        Assert.DoesNotContain(inspections, inspection => inspection.Id == scenario.ThirdInspection.Id);
    }

    [Fact]
    public async Task List_inspections_supports_combined_filters()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);
        var dateFrom = Uri.EscapeDataString("2026-02-01T00:00:00+08:00");
        var dateTo = Uri.EscapeDataString("2026-02-28T23:59:59+08:00");

        var response = await client.GetAsync(
            $"/api/v1/inspections?assetId={scenario.FirstAsset.Id}&scheduleId={scenario.SecondSchedule.Id}&isOperational=false&dateFrom={dateFrom}&dateTo={dateTo}");

        response.EnsureSuccessStatusCode();
        var inspections = await response.Content.ReadFromJsonAsync<List<InspectionResponse>>();

        Assert.NotNull(inspections);
        var inspection = Assert.Single(inspections);
        Assert.Equal(scenario.SecondInspection.Id, inspection.Id);
    }

    [Fact]
    public async Task List_inspections_rejects_an_invalid_date_range()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var dateFrom = Uri.EscapeDataString("2026-03-01T00:00:00+08:00");
        var dateTo = Uri.EscapeDataString("2026-02-01T00:00:00+08:00");

        var response = await client.GetAsync($"/api/v1/inspections?dateFrom={dateFrom}&dateTo={dateTo}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("dateFrom", problem.Errors.Keys);
    }

    [Fact]
    public async Task Get_inspection_by_id_returns_inspection_response()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);

        var response = await client.GetAsync($"/api/v1/inspections/{scenario.SecondInspection.Id}");

        response.EnsureSuccessStatusCode();
        var inspection = await response.Content.ReadFromJsonAsync<InspectionResponse>();

        Assert.NotNull(inspection);
        Assert.Equal(scenario.SecondInspection.Id, inspection.Id);
        Assert.Equal(scenario.SecondSchedule.Id, inspection.ScheduleId);
        Assert.Equal(scenario.FirstAsset.Id, inspection.AssetId);
        Assert.False(inspection.IsOperational);
        Assert.Equal("Needs follow-up", inspection.Remarks);
    }

    [Fact]
    public async Task Get_inspection_by_id_returns_not_found_for_unknown_inspection()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/inspections/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Inspection_history_route_still_returns_asset_history()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var scenario = await CreateScenarioAsync(application, client);

        var response = await client.GetAsync($"/api/v1/inspections/history/{scenario.FirstAsset.Id}");

        response.EnsureSuccessStatusCode();
        var history = await response.Content.ReadFromJsonAsync<List<InspectionHistoryResponse>>();

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);
        Assert.Equal(scenario.SecondInspection.Id, history[0].Id);
        Assert.Equal(scenario.FirstInspection.Id, history[1].Id);
    }

    [Fact]
    public async Task Posting_an_inspection_creates_its_search_document()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-102", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(
            client,
            asset.Id,
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.FromHours(8)));
        var inspection = await CreateInspectionAsync(
            client,
            schedule.Id,
            new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            false,
            "mahina ang pressure");

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var document = await context.MaintenanceSearchDocuments.SingleAsync();

        Assert.Equal(inspection.Id, document.InspectionId);
        Assert.Equal(asset.Id, document.AssetId);
        Assert.Equal("[\"low_pressure\"]", document.IssueKeysJson);
        Assert.Contains("remarks: mahina ang pressure", document.SearchText, StringComparison.Ordinal);

        var persistedInspection = await context.InspectionRecords.SingleAsync();
        var persistedSchedule = await context.PreventiveMaintenanceSchedules.SingleAsync();
        Assert.Equal(schedule.Id, persistedInspection.ScheduleId);
        Assert.Equal(asset.Id, persistedInspection.AssetId);
        Assert.Equal("Completed", persistedSchedule.Status);
        Assert.NotNull(persistedSchedule.CompletedAt);
    }

    [Fact]
    public async Task Posting_a_second_inspection_for_the_same_schedule_returns_conflict_without_extra_records()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-103", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(
            client,
            asset.Id,
            new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.FromHours(8)));
        await CreateInspectionAsync(
            client,
            schedule.Id,
            new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            true,
            "Operational");

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", new
        {
            scheduleId = schedule.Id,
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 5, 16, 8, 0, 0, TimeSpan.FromHours(8)),
            isOperational = true,
            remarks = "Repeated submission"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.InspectionRecords.CountAsync());
        Assert.Equal(1, await context.MaintenanceSearchDocuments.CountAsync());
    }

    [Fact]
    public async Task Posting_an_inspection_for_an_unknown_schedule_returns_not_found()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", new
        {
            scheduleId = Guid.NewGuid(),
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = DateTimeOffset.UtcNow,
            isOperational = true,
            remarks = "Unknown schedule"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<InspectionScenario> CreateScenarioAsync(
        TestApplicationFactory application,
        HttpClient client)
    {
        await application.EnsureAuthenticatedUserAsync();
        var firstAsset = await CreateAssetAsync(client, "FE-101", "fire-extinguisher");
        var secondAsset = await CreateAssetAsync(client, "EL-101", "emergency-light");
        var firstSchedule = await CreateScheduleAsync(
            client,
            firstAsset.Id,
            new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(8)));
        var secondSchedule = await CreateScheduleAsync(
            client,
            firstAsset.Id,
            new DateTimeOffset(2026, 2, 10, 8, 0, 0, TimeSpan.FromHours(8)));
        var thirdSchedule = await CreateScheduleAsync(
            client,
            secondAsset.Id,
            new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.FromHours(8)));

        var firstInspection = await CreateInspectionAsync(
            client,
            firstSchedule.Id,
            new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            true,
            "Operational");
        var secondInspection = await CreateInspectionAsync(
            client,
            secondSchedule.Id,
            new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            false,
            "Needs follow-up");
        var thirdInspection = await CreateInspectionAsync(
            client,
            thirdSchedule.Id,
            new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            true,
            "Operational");

        return new InspectionScenario(
            firstAsset,
            secondAsset,
            firstSchedule,
            secondSchedule,
            thirdSchedule,
            firstInspection,
            secondInspection,
            thirdInspection);
    }

    private static async Task<AssetResponse> CreateAssetAsync(
        HttpClient client,
        string assetCode,
        string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory,
            building = "Main",
            department = "GSD",
            location = "Inspection Area"
        });

        response.EnsureSuccessStatusCode();
        var asset = await response.Content.ReadFromJsonAsync<AssetResponse>();
        Assert.NotNull(asset);
        return asset;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(
        HttpClient client,
        Guid assetId,
        DateTimeOffset scheduleDate)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate,
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        response.EnsureSuccessStatusCode();
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        return schedule;
    }

    private static async Task<InspectionResponse> CreateInspectionAsync(
        HttpClient client,
        Guid scheduleId,
        DateTimeOffset dateInspected,
        bool isOperational,
        string remarks)
    {
        var response = await client.PostAsJsonAsync("/api/v1/inspections/", new
        {
            scheduleId,
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected,
            isOperational,
            remarks
        });

        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        Assert.False(document.RootElement.TryGetProperty("remarksEmbedding", out _));
        Assert.False(document.RootElement.TryGetProperty("asset", out _));
        Assert.False(document.RootElement.TryGetProperty("schedule", out _));

        var inspection = JsonSerializer.Deserialize<InspectionResponse>(
            responseBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(inspection);
        return inspection;
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-inspections-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(AuthRoleCatalog.Gsd);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }

        public async Task EnsureAuthenticatedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            if (await context.Users.AnyAsync(user => user.Id == TestAuthenticationHandler.UserId))
            {
                return;
            }

            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "test-user@unipm.local",
                NormalizedUserName = "TEST-USER@UNIPM.LOCAL",
                Email = "test-user@unipm.local",
                NormalizedEmail = "TEST-USER@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "Test User",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }

    private sealed record InspectionScenario(
        AssetResponse FirstAsset,
        AssetResponse SecondAsset,
        ScheduleResponse FirstSchedule,
        ScheduleResponse SecondSchedule,
        ScheduleResponse ThirdSchedule,
        InspectionResponse FirstInspection,
        InspectionResponse SecondInspection,
        InspectionResponse ThirdInspection);

    private sealed record AssetResponse(
        Guid Id,
        string AssetCode,
        string AssetCategory,
        string? Building,
        string? Department,
        string? Location,
        string? QrCodeValue,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record ScheduleResponse(
        Guid Id,
        Guid AssetId,
        DateTimeOffset ScheduleDate,
        string PeriodType,
        string Status,
        string? Quarter,
        string? Semester,
        int? Year,
        string? AcademicYear,
        Guid? AssignedToUserId,
        DateTimeOffset? CompletedAt,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        object? Asset);

    private sealed record InspectionHistoryResponse(
        Guid Id,
        DateTimeOffset DateInspected,
        bool IsOperational,
        string? Remarks,
        string? ActionsRecommendations);
}
