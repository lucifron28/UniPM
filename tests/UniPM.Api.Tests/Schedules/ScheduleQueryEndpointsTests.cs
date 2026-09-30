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
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class ScheduleQueryEndpointsTests
{
    [Fact]
    public async Task List_schedules_returns_schedules_matching_filters()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var fireExtinguisher = await CreateAssetAsync(client, "FE-010", "fire-extinguisher");
        var emergencyLight = await CreateAssetAsync(client, "EL-010", "emergency-light");

        var targetSchedule = await CreateScheduleAsync(
            client,
            fireExtinguisher.Id,
            new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero),
            " quarter ",
            " q1 ",
            2026);

        await CreateScheduleAsync(
            client,
            emergencyLight.Id,
            new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero),
            "Quarter",
            "Q2",
            2026);

        var from = Uri.EscapeDataString("2026-01-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-03-31T23:59:59Z");
        var response = await client.GetAsync(
            $"/api/v1/schedules?assetId={fireExtinguisher.Id}&status=Due&from={from}&to={to}&quarter=Q1&year=2026");

        response.EnsureSuccessStatusCode();
        var schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>();

        Assert.NotNull(schedules);
        var schedule = Assert.Single(schedules);
        Assert.Equal(targetSchedule.Id, schedule.Id);
        Assert.Equal(fireExtinguisher.Id, schedule.AssetId);
        Assert.Equal("Quarter", schedule.PeriodType);
        Assert.Equal("Q1", schedule.Quarter);
        Assert.Equal(2026, schedule.Year);
        Assert.NotNull(targetSchedule.Asset);
        Assert.Equal("FE-010", targetSchedule.Asset.AssetCode);
    }

    [Fact]
    public async Task Get_schedule_by_id_returns_schedule_response_with_asset_summary()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var asset = await CreateAssetAsync(client, "WDS-010", "water-drinking-station");
        var createdSchedule = await CreateScheduleAsync(
            client,
            asset.Id,
            new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero),
            "Quarter",
            "Q3",
            2026);

        var response = await client.GetAsync($"/api/v1/schedules/{createdSchedule.Id}");

        response.EnsureSuccessStatusCode();
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();

        Assert.NotNull(schedule);
        Assert.Equal(createdSchedule.Id, schedule.Id);
        Assert.Equal(asset.Id, schedule.AssetId);
        Assert.NotNull(schedule.Asset);
        Assert.Equal("WDS-010", schedule.Asset.AssetCode);
    }

    [Fact]
    public async Task Get_schedule_by_id_returns_not_found_for_unknown_schedule()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var response = await client.GetAsync($"/api/v1/schedules/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_schedules_rejects_invalid_date_range()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var from = Uri.EscapeDataString("2026-12-31T00:00:00Z");
        var to = Uri.EscapeDataString("2026-01-01T00:00:00Z");
        var response = await client.GetAsync($"/api/v1/schedules?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("from", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_schedules_rejects_unsupported_code_filters()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();

        var statusResponse = await client.GetAsync("/api/v1/schedules?status=Paused");
        var quarterResponse = await client.GetAsync("/api/v1/schedules?quarter=Q5");

        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, quarterResponse.StatusCode);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Retired")]
    public async Task Create_schedule_rejects_non_active_asset(string status)
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, $"FE-{status}", "fire-extinguisher");
        await using (var context = await application.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            var stored = await context.Assets.SingleAsync(candidate => candidate.Id == asset.Id);
            stored.Status = status;
            await context.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("AssetId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_schedule_rejects_legacy_asset_without_department()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var assetId = Guid.NewGuid();
        await using (var context = await application.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            context.Assets.Add(new Asset
            {
                Id = assetId,
                AssetCode = "FE-LEGACY",
                AssetCategory = "fire-extinguisher",
                Status = "Active"
            });
            await context.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("AssetId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_schedule_derives_year_and_quarter_and_rejects_conflicting_values()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, "FE-TEMPORAL", "fire-extinguisher");
        var scheduleDate = new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.FromHours(14));

        var created = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate,
            periodType = "Quarter"
        });
        created.EnsureSuccessStatusCode();
        var schedule = await created.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal("2026-12", schedule.PmCycle);
        Assert.Equal(2026, schedule.Year);
        Assert.Equal("Q4", schedule.Quarter);

        var conflicting = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate,
            periodType = "Quarter",
            year = 2027,
            quarter = "Q1"
        });
        Assert.Equal(HttpStatusCode.BadRequest, conflicting.StatusCode);
        var problem = await conflicting.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Year", problem.Errors.Keys);
        Assert.Contains("Quarter", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_schedule_normalizes_stale_quarter_for_non_quarter_period()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(
            client,
            "FE-ANNUAL",
            "fire-extinguisher");
        var scheduleDate = new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.FromHours(14));

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate,
            periodType = "Annual",
            quarter = "Q1"
        });

        response.EnsureSuccessStatusCode();
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal("2026-12", schedule.PmCycle);
        Assert.Equal(2026, schedule.Year);
        Assert.Null(schedule.Quarter);
    }

    [Fact]
    public async Task Create_schedule_rejects_derived_year_outside_planning_range_without_year_input()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(
            client,
            "FE-FUTURE-YEAR",
            "fire-extinguisher");
        var unsupportedYear = DateTimeOffset.UtcNow.Year + 6;

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate = new DateTimeOffset(
                unsupportedYear,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.FromHours(8)),
            periodType = "Annual"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Year", problem.Errors.Keys);
        var maxPlanningYear = DateTimeOffset.UtcNow.Year + 5;
        Assert.Contains(
            problem.Errors["Year"],
            message => message.Contains(maxPlanningYear.ToString()));
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
            location = "Lobby"
        });

        response.EnsureSuccessStatusCode();

        var asset = await response.Content.ReadFromJsonAsync<AssetResponse>();
        Assert.NotNull(asset);
        return asset;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(
        HttpClient client,
        Guid assetId,
        DateTimeOffset scheduleDate,
        string periodType,
        string quarter,
        int year)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate,
            periodType,
            quarter,
            year
        });

        response.EnsureSuccessStatusCode();

        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        return schedule;
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-schedules-{Guid.NewGuid()}";

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
    }

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
        string PmCycle,
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
        ScheduleAssetResponse? Asset);

    private sealed record ScheduleAssetResponse(
        Guid Id,
        string AssetCode,
        string AssetCategory,
        string? Building,
        string? Department,
        string? Location);
}
