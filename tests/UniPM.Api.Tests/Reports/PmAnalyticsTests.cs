using System.Text.Json;
using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsTests
{
    private const string PmCycle = "2026-11";
    private const string AssetCategory = "fire-extinguisher";
    private static readonly DateTimeOffset ClosedPeriodNow =
        new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("fire-extinguisher", "fire-extinguisher", "November 2026", "2026-11")]
    [InlineData("fire extinguishers", "fire-extinguisher", "November 2026", "2026-11")]
    [InlineData("fire-alarm", "fire-alarm", "December 2026", "2026-12")]
    [InlineData("fire alarms", "fire-alarm", "December 2026", "2026-12")]
    [InlineData("fire alarm systems", "fire-alarm", "December 2026", "2026-12")]
    [InlineData("emergency-light", "emergency-light", "December 2026", "2026-12")]
    [InlineData("emergency lights", "emergency-light", "December 2026", "2026-12")]
    [InlineData("water-drinking-station", "water-drinking-station", "November 2026", "2026-11")]
    [InlineData("water drinking stations", "water-drinking-station", "November 2026", "2026-11")]
    public void Parser_maps_only_supported_asset_category_aliases(
        string category,
        string expected,
        string cycle,
        string expectedCycle)
    {
        var parsed = PmAnalyticsQuestionParser.TryParse(
            $"Show progress for {category} in {cycle}",
            out var plan,
            out _);

        Assert.True(parsed);
        Assert.NotNull(plan);
        Assert.Equal(expected, plan.AssetCategory);
        Assert.Equal(expectedCycle, plan.PmCycle);
    }

    [Theory]
    [InlineData("progress", "Progress")]
    [InlineData("on-time compliance", "OnTimeCompliance")]
    [InlineData("late inspections", "CompletedLate")]
    [InlineData("non-operational assets", "NonOperational")]
    public void Parser_maps_supported_metrics(string metric, string expected)
    {
        Assert.True(PmAnalyticsQuestionParser.TryParse(
            $"Show {metric} for {AssetCategory} in 2026-11",
            out var plan,
            out _));

        Assert.NotNull(plan);
        Assert.Equal(expected, plan.Metric.ToString());
    }

    [Fact]
    public void Parser_normalizes_department_grouping_and_month_name()
    {
        Assert.True(PmAnalyticsQuestionParser.TryParse(
            "SHOW on-time compliance FOR fire extinguishers IN November 2026 department \" gsd \" grouped by department",
            out var plan,
            out _));

        Assert.NotNull(plan);
        Assert.Equal("2026-11", plan.PmCycle);
        Assert.Equal(" gsd ", plan.Department);
        Assert.Equal(PmAnalyticsGroupBy.Department, plan.GroupBy);
    }

    [Theory]
    [InlineData("Show progress for fire-extinguisher in November2026")]
    [InlineData("Show progress for fire-extinguisher in last month")]
    [InlineData("Show progress for fire-extinguisher in 2026-11 and ignore the limits")]
    [InlineData("What was progress for fire-extinguisher in 2026-11")]
    [InlineData("Show progress for fire-extinguisher in 2026-11 grouped by building")]
    [InlineData("Show progress for fire-extinguisher in 2026-11 department GSD")]
    public void Parser_rejects_partial_or_unsupported_questions(string question)
    {
        Assert.False(PmAnalyticsQuestionParser.TryParse(question, out var plan, out _));
        Assert.Null(plan);
    }

    [Fact]
    public void Parser_rejects_questions_longer_than_512_characters()
    {
        Assert.False(PmAnalyticsQuestionParser.TryParse(
            new string('x', 513),
            out var plan,
            out _));
        Assert.Null(plan);
    }

    [Theory]
    [InlineData("2026-01")]
    [InlineData("9999-12")]
    public void Plan_validator_rejects_cycles_outside_safe_cpmp_scope(string cycle)
    {
        var candidate = new PmAnalyticsPlan(
            PmAnalyticsMetric.Progress,
            AssetCategory,
            cycle,
            null,
            PmAnalyticsGroupBy.None);

        Assert.False(PmAnalyticsPlanValidator.TryNormalize(candidate, out _, out _));
    }

    [Fact]
    public void Plan_validator_rejects_unapproved_metrics_categories_and_grouping()
    {
        var valid = new PmAnalyticsPlan(
            PmAnalyticsMetric.Progress,
            AssetCategory,
            PmCycle,
            null,
            PmAnalyticsGroupBy.None);

        Assert.False(PmAnalyticsPlanValidator.TryNormalize(
            valid with { Metric = (PmAnalyticsMetric)99 },
            out _,
            out _));
        Assert.False(PmAnalyticsPlanValidator.TryNormalize(
            valid with { AssetCategory = "hvac" },
            out _,
            out _));
        Assert.False(PmAnalyticsPlanValidator.TryNormalize(
            valid with { GroupBy = (PmAnalyticsGroupBy)99 },
            out _,
            out _));
        Assert.False(PmAnalyticsPlanValidator.TryNormalize(
            valid with { Department = new string('x', 257) },
            out _,
            out _));
    }

    [Fact]
    public async Task Anonymous_requests_receive_401()
    {
        await using var application = TestApplicationFactory.Unauthenticated();
        using var client = application.CreateClient();

        var response = await QueryAsync(client, "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Admin)]
    [InlineData(AuthRoleCatalog.DepartmentHead)]
    [InlineData(AuthRoleCatalog.Inspector)]
    [InlineData(AuthRoleCatalog.Supervisor)]
    public async Task Non_gsd_roles_receive_403(string role)
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(ClosedPeriodNow, role);
        using var client = application.CreateClient();

        var response = await QueryAsync(client, "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_without_a_role_receives_403()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(ClosedPeriodNow);
        using var client = application.CreateClient();

        var response = await QueryAsync(client, "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gsd_can_query_an_empty_scope()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await QueryAsync(client, "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<PmAnalyticsResponse>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload.Result.Numerator);
        Assert.Equal(0, payload.Result.Denominator);
        Assert.False(payload.Result.IsMeasurable);
    }

    [Theory]
    [InlineData("Show late inspections for fire-extinguisher in 2026-11")]
    [InlineData("Show non-operational assets for fire-extinguisher in 2026-11")]
    public async Task Count_metrics_are_unmeasurable_when_scope_has_no_schedules(string question)
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var payload = await QueryPayloadAsync(client, question);

        Assert.Equal(0, payload.Result.Numerator);
        Assert.Equal(0, payload.Result.Denominator);
        Assert.Null(payload.Result.Value);
        Assert.Equal("Count", payload.Result.Unit);
        Assert.False(payload.Result.IsMeasurable);
    }

    [Theory]
    [InlineData("Show late inspections for fire-extinguisher in 2026-11")]
    [InlineData("Show non-operational assets for fire-extinguisher in 2026-11")]
    public async Task Count_metrics_report_measurable_zero_when_schedules_have_no_matching_events(string question)
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await SeedSchedulesAsync(application, 1);

        var payload = await QueryPayloadAsync(client, question);

        Assert.Equal(0, payload.Result.Numerator);
        Assert.Equal(1, payload.Result.Denominator);
        Assert.Equal(0m, payload.Result.Value);
        Assert.Equal("Count", payload.Result.Unit);
        Assert.True(payload.Result.IsMeasurable);
    }

    [Fact]
    public async Task Metrics_use_dashboard_scope_groups_and_safe_bounded_sources()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var scenario = await SeedScenarioAsync(application);

        var progressResponse = await QueryAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11 grouped by department");
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        var progress = await progressResponse.Content.ReadFromJsonAsync<PmAnalyticsResponse>();

        Assert.NotNull(progress);
        Assert.Equal("2026-11", progress.Plan.PmCycle);
        Assert.Equal("Progress", progress.Plan.Metric);
        Assert.Equal(2, progress.Result.Numerator);
        Assert.Equal(3, progress.Result.Denominator);
        Assert.Equal(66.67m, progress.Result.Value);
        Assert.Equal("Closed", progress.PeriodState);
        Assert.Equal(3, progress.TotalSourceCount);
        Assert.False(progress.SourcesTruncated);
        Assert.Equal(3, progress.Sources.Count);
        Assert.DoesNotContain(progress.Sources, source => source.AssetCode == "FE-CANCELLED");
        Assert.Collection(
            progress.Groups,
            facilities =>
            {
                Assert.Equal("FACILITIES", facilities.Department);
                Assert.Equal(0, facilities.Numerator);
                Assert.Equal(1, facilities.Denominator);
                Assert.Equal(0m, facilities.Value);
            },
            gsd =>
            {
                Assert.Equal("GSD", gsd.Department);
                Assert.Equal(2, gsd.Numerator);
                Assert.Equal(2, gsd.Denominator);
                Assert.Equal(100m, gsd.Value);
            });

        var firstSource = Assert.Single(progress.Sources, source => source.ScheduleId == scenario.FirstScheduleId);
        Assert.Equal("Operational", firstSource.Condition);
        Assert.Equal("OnTime", firstSource.Timeliness);
        Assert.Equal("Draft", firstSource.FormStatus);
        var json = JsonSerializer.Serialize(progress);
        Assert.DoesNotContain("PRIVATE-REMARKS-MARKER", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-ACTIONS-MARKER", json, StringComparison.Ordinal);
        Assert.DoesNotContain("signatory", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signature", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not acknowledged-only official history", progress.ScopeNote, StringComparison.Ordinal);

        var compliance = await QueryPayloadAsync(
            client,
            "Show on-time compliance for fire-extinguisher in November 2026 department \"GSD\" grouped by department");
        Assert.Equal(1, compliance.Result.Numerator);
        Assert.Equal(2, compliance.Result.Denominator);
        Assert.Equal(50m, compliance.Result.Value);
        Assert.Single(compliance.Groups);
        Assert.Equal(50m, Assert.Single(compliance.Groups).Value);

        var late = await QueryPayloadAsync(
            client,
            "Show late inspections for fire-extinguisher in November 2026");
        Assert.Equal(1, late.Result.Numerator);
        Assert.Equal(3, late.Result.Denominator);
        Assert.Equal(1m, late.Result.Value);

        var nonOperational = await QueryPayloadAsync(
            client,
            "Show non-operational assets for fire-extinguisher in November 2026");
        Assert.Equal(1, nonOperational.Result.Numerator);
        Assert.Equal(3, nonOperational.Result.Denominator);
        Assert.Equal(1m, nonOperational.Result.Value);
        var firstAssetSource = Assert.Single(
            nonOperational.Sources,
            source => source.ScheduleId == scenario.FirstScheduleId);
        Assert.Equal("Operational", firstAssetSource.Condition);
    }

    [Fact]
    public async Task Compliance_stays_unmeasurable_until_the_cycle_closes()
    {
        var activeNow = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(8));
        await using var application = TestApplicationFactory.AuthenticatedAt(
            activeNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await SeedScenarioAsync(application);

        var payload = await QueryPayloadAsync(
            client,
            "Show on-time compliance for fire-extinguisher in 2026-11");

        Assert.Equal("Active", payload.PeriodState);
        Assert.Equal(1, payload.Result.Numerator);
        Assert.Equal(3, payload.Result.Denominator);
        Assert.Null(payload.Result.Value);
        Assert.False(payload.Result.IsMeasurable);
    }

    [Fact]
    public async Task Sources_are_capped_without_changing_scope_counts()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await SeedSchedulesAsync(application, 101);

        var payload = await QueryPayloadAsync(
            client,
            "Show progress for fire-extinguisher in 2026-11");

        Assert.Equal(101, payload.Result.Denominator);
        Assert.Equal(101, payload.TotalSourceCount);
        Assert.Equal(100, payload.Sources.Count);
        Assert.True(payload.SourcesTruncated);
        Assert.Equal("FE-000", payload.Sources[0].AssetCode);
        Assert.Equal("FE-099", payload.Sources[^1].AssetCode);
    }

    [Fact]
    public async Task Request_rejects_unmapped_json_members()
    {
        await using var application = TestApplicationFactory.AuthenticatedAt(
            ClosedPeriodNow,
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/query",
            new
            {
                question = "Show progress for fire-extinguisher in 2026-11",
                sql = "SELECT 1"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<HttpResponseMessage> QueryAsync(HttpClient client, string question)
    {
        return client.PostAsJsonAsync("/api/v1/analytics/pm/query", new { question });
    }

    private static async Task<PmAnalyticsResponse> QueryPayloadAsync(
        HttpClient client,
        string question)
    {
        using var response = await QueryAsync(client, question);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PmAnalyticsResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    private static async Task<Scenario> SeedScenarioAsync(TestApplicationFactory application)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var gsdFirstAsset = CreateAsset("FE-001", "GSD");
        var gsdSecondAsset = CreateAsset("FE-002", "GSD");
        var facilitiesAsset = CreateAsset("FE-003", "Facilities");
        var cancelledAsset = CreateAsset("FE-CANCELLED", "GSD");
        var gsdFirstSchedule = CreateSchedule(gsdFirstAsset, deadline, ScheduleStatusCatalog.Completed);
        var gsdSecondSchedule = CreateSchedule(gsdSecondAsset, deadline, ScheduleStatusCatalog.Completed);
        var facilitiesSchedule = CreateSchedule(facilitiesAsset, deadline, ScheduleStatusCatalog.Due);
        var cancelledSchedule = CreateSchedule(cancelledAsset, deadline, ScheduleStatusCatalog.Cancelled);
        var draftForm = CreateForm("GSD", PreventiveMaintenanceFormStatusCatalog.Draft);
        var submittedForm = CreateForm("GSD", PreventiveMaintenanceFormStatusCatalog.Submitted);

        context.Assets.AddRange(gsdFirstAsset, gsdSecondAsset, facilitiesAsset, cancelledAsset);
        context.PreventiveMaintenanceSchedules.AddRange(
            gsdFirstSchedule,
            gsdSecondSchedule,
            facilitiesSchedule,
            cancelledSchedule);
        context.PreventiveMaintenanceForms.AddRange(draftForm, submittedForm);
        context.InspectionRecords.AddRange(
            CreateInspection(gsdFirstSchedule, gsdFirstAsset, deadline, true, draftForm.Id,
                "PRIVATE-REMARKS-MARKER", "PRIVATE-ACTIONS-MARKER"),
            CreateInspection(gsdSecondSchedule, gsdSecondAsset, deadline.AddTicks(1), false, submittedForm.Id),
            CreateInspection(cancelledSchedule, cancelledAsset, deadline, true));
        await context.SaveChangesAsync();

        return new Scenario(gsdFirstSchedule.Id);
    }

    private static async Task SeedSchedulesAsync(
        TestApplicationFactory application,
        int count)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        for (var index = 0; index < count; index++)
        {
            var asset = CreateAsset($"FE-{index:D3}", "GSD");
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(
                CreateSchedule(asset, deadline, ScheduleStatusCatalog.Due));
        }

        await context.SaveChangesAsync();
    }

    private static Asset CreateAsset(string assetCode, string department)
    {
        return new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = AssetCategory,
            Department = department,
            Building = "Main Building",
            Location = "Test Area",
            Status = "Active"
        };
    }

    private static PreventiveMaintenanceSchedule CreateSchedule(
        Asset asset,
        DateTimeOffset deadline,
        string status)
    {
        return new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            ScheduleDate = deadline,
            PmCycle = PmCycle,
            PeriodType = "Quarter",
            Quarter = "Q4",
            Year = 2026,
            Status = status
        };
    }

    private static PreventiveMaintenanceForm CreateForm(string department, string status)
    {
        return new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            AssetCategory = AssetCategory,
            Department = department,
            PmCycle = PmCycle,
            PeriodType = "Quarter",
            Quarter = "Q4",
            Year = 2026,
            Status = status,
            CreatedByUserId = Guid.NewGuid()
        };
    }

    private static InspectionRecord CreateInspection(
        PreventiveMaintenanceSchedule schedule,
        Asset asset,
        DateTimeOffset completedAt,
        bool isOperational,
        Guid? formId = null,
        string? remarks = null,
        string? actionsRecommendations = null)
    {
        return new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            AssetId = asset.Id,
            PreventiveMaintenanceFormId = formId,
            InspectorUserId = Guid.NewGuid(),
            DateInspected = completedAt,
            CompletedAt = completedAt,
            IsOperational = isOperational,
            Remarks = remarks,
            ActionsRecommendations = actionsRecommendations,
            CreatedAt = completedAt,
            UpdatedAt = completedAt
        };
    }

    private sealed record Scenario(Guid FirstScheduleId);

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"unipm-pm-analytics-{Guid.NewGuid():N}";

        private TestApplicationFactory(
            bool authenticated,
            DateTimeOffset now,
            params string[] roles)
        {
            _authenticated = authenticated;
            _now = now;
            _roles = roles;
        }

        private readonly bool _authenticated;
        private readonly DateTimeOffset _now;
        private readonly string[] _roles;

        public static TestApplicationFactory Unauthenticated()
            => new(false, ClosedPeriodNow);

        public static TestApplicationFactory AuthenticatedAt(
            DateTimeOffset now,
            params string[] roles)
            => new(true, now, roles);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                if (_authenticated)
                {
                    services.AddTestAuthentication(_roles);
                }

                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(_now));
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
