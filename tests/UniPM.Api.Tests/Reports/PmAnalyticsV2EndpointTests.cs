using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV2EndpointTests
{
    [Fact]
    public async Task Versioned_endpoints_validate_typed_plans_and_preserve_role_and_legacy_contracts()
    {
        await using var gsdApplication = new AnalyticsApplicationFactory(authenticated: true, AuthRoleCatalog.Gsd);
        using var gsdClient = gsdApplication.CreateClient();
        await EnsureCreatedAsync(gsdApplication.Services);

        using (var valid = await gsdClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(ValidPlan())))
        {
            Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
            var result = await valid.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
            Assert.NotNull(result);
            Assert.Equal(2, result.SchemaVersion);
            Assert.Equal(0, result.Population.EligibleAssetCycleCount);
            Assert.Null(result.Result!.Value);
            Assert.False(result.Result.IsMeasurable);
        }

        using (var invalidPlan = await gsdClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(ValidPlan() with { SchemaVersion = 1 })))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidPlan.StatusCode);
        }

        using (var unknownContext = await gsdClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(ValidPlan() with { Department = "Unknown department" })))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknownContext.StatusCode);
        }

        using (var unknownField = await gsdClient.PostAsync(
            "/api/v1/analytics/pm/v2/query",
            Json("""
                {"plan":{"schemaVersion":2,"metric":"Progress","assetCategory":"fire-extinguisher","period":{"kind":"Year","year":2025},"comparison":null,"presentation":"Count","countBasis":"AssetCycles","groupBy":"None","unexpected":"reject-me"},"sourceScope":"Matching","page":1,"pageSize":25}
                """)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        }

        using (var missingMetric = await gsdClient.PostAsync(
            "/api/v1/analytics/pm/v2/query",
            Json("""
                {"plan":{"schemaVersion":2,"assetCategory":"fire-extinguisher","period":{"kind":"Year","year":2025},"presentation":"Count","countBasis":"AssetCycles","groupBy":"None"}}
                """)))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingMetric.StatusCode);
        }

        using (var unsupported = await gsdClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/interpret",
            new PmAnalyticsV2InterpretRequest(
                "Predict the repair cost for fire extinguishers in November 2025")))
        {
            Assert.Equal(HttpStatusCode.OK, unsupported.StatusCode);
            var interpretation = await unsupported.Content.ReadFromJsonAsync<PmAnalyticsV2InterpretationResponse>();
            Assert.NotNull(interpretation);
            Assert.Equal("Unsupported", interpretation.Status);
            Assert.Null(interpretation.Plan);
        }

        using (var legacy = await gsdClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/query",
            new PmAnalyticsQuestionRequest("Show progress for fire extinguishers in November 2025")))
        {
            Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
            Assert.NotNull(await legacy.Content.ReadFromJsonAsync<PmAnalyticsResponse>());
        }

        await using var supervisorApplication = new AnalyticsApplicationFactory(authenticated: true, AuthRoleCatalog.Supervisor);
        using var supervisorClient = supervisorApplication.CreateClient();
        using var forbidden = await supervisorClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(ValidPlan()));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await using var anonymousApplication = new AnalyticsApplicationFactory(authenticated: false);
        using var anonymousClient = anonymousApplication.CreateClient();
        using var unauthorized = await anonymousClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(ValidPlan()));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Blank_stored_pm_cycle_uses_schedule_date_fallback_in_memory()
    {
        await using var application = new AnalyticsApplicationFactory(authenticated: true, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await EnsureCreatedAsync(application.Services);

        var assetId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.FromHours(8));
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.Assets.Add(new Asset
            {
                Id = assetId,
                AssetCode = "V2-LEGACY-CYCLE",
                AssetCategory = "fire-extinguisher",
                Building = "Legacy Test Building",
                Department = "Legacy Test Department",
                Location = "Synthetic test area",
                Status = AssetStatusCatalog.Active,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
            context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                ScheduleDate = createdAt,
                // InMemory-only legacy row. Native SQL enforces canonical PM cycle values.
                PmCycle = string.Empty,
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = 2023,
                Status = ScheduleStatusCatalog.Due,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
            await context.SaveChangesAsync();
        }

        var plan = ValidPlan() with
        {
            Period = new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Month, Year: 2023, Month: 2)
        };
        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v2/query",
            new PmAnalyticsV2QueryRequest(plan, PmAnalyticsV2SourceScope.Calculation));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PmAnalyticsV2QueryResponse>();
        Assert.NotNull(result);
        var source = Assert.Single(result.Sources);
        Assert.Equal("2023-02", source.PmCycle);
        Assert.True(source.UsedLegacyDateFallback);
        Assert.Equal(PreventiveMaintenanceCycle.DeadlineForCycle("2023-02"), source.Deadline);
    }

    private static async Task EnsureCreatedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private static PmAnalyticsV2Plan ValidPlan() => new(
        2,
        PmAnalyticsV2Metric.Progress,
        "fire-extinguisher",
        new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
        null,
        PmAnalyticsV2Presentation.Percent,
        PmAnalyticsV2CountBasis.AssetCycles,
        null,
        null,
        null,
        null,
        null,
        PmAnalyticsV2GroupBy.None,
        null);

    private static StringContent Json(string content) =>
        new(content, Encoding.UTF8, "application/json");

    private sealed class AnalyticsApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"PmAnalyticsV2Endpoint_{Guid.NewGuid():N}";
        private readonly bool authenticated;
        private readonly string[] roles;

        public AnalyticsApplicationFactory(bool authenticated, params string[] roles)
        {
            this.authenticated = authenticated;
            this.roles = roles;
        }

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
                if (authenticated)
                {
                    services.AddTestAuthentication(roles);
                }

                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
