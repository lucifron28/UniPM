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
            new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.Zero),
            " quarter ",
            " q1 ",
            2026);

        await CreateScheduleAsync(
            client,
            emergencyLight.Id,
            new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero),
            "Quarter",
            "Q2",
            2026);

        var from = Uri.EscapeDataString("2026-02-01T00:00:00Z");
        var to = Uri.EscapeDataString("2026-02-28T23:59:59Z");
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
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero),
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
            scheduleDate = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(8)),
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
            scheduleDate = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(8)),
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
        var scheduleDate = new DateTimeOffset(2026, 6, 1, 0, 30, 0, TimeSpan.FromHours(14));

        var created = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate,
            periodType = "Quarter"
        });
        created.EnsureSuccessStatusCode();
        var schedule = await created.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal("2026-05", schedule.PmCycle);
        Assert.Equal(2026, schedule.Year);
        Assert.Equal("Q2", schedule.Quarter);
        Assert.Equal(Deadline(2026, 5, 31), schedule.ScheduleDate);

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
        var scheduleDate = new DateTimeOffset(2026, 6, 1, 0, 30, 0, TimeSpan.FromHours(14));

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
        Assert.Equal("2026-05", schedule.PmCycle);
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
                11,
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

    [Theory]
    [InlineData("fire-extinguisher", "2026-02", "Quarter", 28, "Q1")]
    [InlineData("fire-alarm", "2026-06", "Semester", 30, null)]
    [InlineData("emergency-light", "2026-12", "Semester", 31, null)]
    [InlineData("water-drinking-station", "2026-11", "Quarter", 30, "Q4")]
    public async Task Create_schedule_accepts_a_valid_cpmp_cycle_and_derives_its_deadline(
        string assetCategory,
        string pmCycle,
        string periodType,
        int lastDay,
        string? quarter)
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, $"CYCLE-{Guid.NewGuid():N}"[..12], assetCategory);

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle,
            periodType
        });

        response.EnsureSuccessStatusCode();
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal(pmCycle, schedule.PmCycle);
        Assert.Equal(2026, schedule.Year);
        Assert.Equal(quarter, schedule.Quarter);
        Assert.Equal(Deadline(2026, int.Parse(pmCycle[^2..]), lastDay), schedule.ScheduleDate);
    }

    [Theory]
    [InlineData("fire-extinguisher", "2026-06")]
    [InlineData("fire-alarm", "2026-05")]
    [InlineData("emergency-light", "2026-08")]
    [InlineData("water-drinking-station", "2026-06")]
    public async Task Create_schedule_rejects_a_cycle_month_outside_the_asset_category_frequency(
        string assetCategory,
        string pmCycle)
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, $"INVALID-{Guid.NewGuid():N}"[..12], assetCategory);

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle,
            periodType = "Quarter"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PmCycle", problem.Errors.Keys);
        Assert.Contains(problem.Errors["PmCycle"], message => message.Contains("PM cycle month"));
    }

    [Theory]
    [InlineData("fire-extinguisher", "2028-02", 29)]
    [InlineData("fire-alarm", "2026-06", 30)]
    [InlineData("fire-alarm", "2026-12", 31)]
    public async Task Create_schedule_uses_the_calendar_month_end_for_the_pm_cycle(
        string assetCategory,
        string pmCycle,
        int lastDay)
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, $"MONTH-{Guid.NewGuid():N}"[..12], assetCategory);

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle,
            periodType = "Semester"
        });

        response.EnsureSuccessStatusCode();
        var schedule = await response.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(schedule);
        Assert.Equal(Deadline(int.Parse(pmCycle[..4]), int.Parse(pmCycle[^2..]), lastDay), schedule.ScheduleDate);
    }

    [Fact]
    public async Task Create_schedule_rejects_invalid_or_inconsistent_pm_cycles()
    {
        await using var application = new TestApplicationFactory();
        var client = application.CreateClient();
        var asset = await CreateAssetAsync(client, "FE-CYCLE-MISMATCH", "fire-extinguisher");

        var invalid = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-13",
            periodType = "Quarter"
        });
        var mismatch = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-02",
            scheduleDate = new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter"
        });
        var matching = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            pmCycle = "2026-02",
            scheduleDate = new DateTimeOffset(2026, 2, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter"
        });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        matching.EnsureSuccessStatusCode();
        var matchingSchedule = await matching.Content.ReadFromJsonAsync<ScheduleResponse>();
        Assert.NotNull(matchingSchedule);
        Assert.Equal("2026-02", matchingSchedule.PmCycle);
        Assert.Equal(Deadline(2026, 2, 28), matchingSchedule.ScheduleDate);
        var invalidProblem = await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        var mismatchProblem = await mismatch.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(invalidProblem);
        Assert.NotNull(mismatchProblem);
        Assert.Contains("PmCycle", invalidProblem.Errors.Keys);
        Assert.Contains("PmCycle", mismatchProblem.Errors.Keys);
    }

    private static DateTimeOffset Deadline(int year, int month, int lastDay)
    {
        return new DateTimeOffset(year, month, lastDay, 23, 59, 59, TimeSpan.FromHours(8))
            .AddTicks(TimeSpan.TicksPerSecond - 1);
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
