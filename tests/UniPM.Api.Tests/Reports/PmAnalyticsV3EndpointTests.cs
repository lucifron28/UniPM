using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3EndpointTests
{
    [Fact]
    public async Task Gsd_can_interpret_preview_and_execute_without_a_provider_call()
    {
        await using var application = new AnalyticsApplicationFactory(authenticated: true, AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await SeedAsync(application.Services);

        using var catalogResponse = await client.GetAsync("/api/v1/analytics/pm/v3/catalog");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            PmAnalyticsV3Catalog.Version,
            catalog.GetProperty("catalogVersion").GetInt32());
        Assert.Equal(
            JsonValueKind.Array,
            catalog.GetProperty("filterFields")[0].GetProperty("operators").ValueKind);

        using var valuesResponse = await client.GetAsync(
            "/api/v1/analytics/pm/v3/catalog/values?field=department&categories=fire-extinguisher&search=Synthetic");
        Assert.Equal(HttpStatusCode.OK, valuesResponse.StatusCode);
        var values = await valuesResponse.Content.ReadFromJsonAsync<PmAnalyticsV3FilterValuesResponse>();
        Assert.NotNull(values);
        Assert.Contains("Synthetic GSD", values.Values);

        using var interpretationResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show scheduled fire extinguishers during 2024"));
        Assert.Equal(HttpStatusCode.OK, interpretationResponse.StatusCode);
        var interpretation = await interpretationResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation);
        Assert.Equal("Valid", interpretation.Status);
        Assert.NotNull(interpretation.Plan);

        using var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/preview",
            new PmAnalyticsV3PreviewRequest(interpretation.Plan));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<PmAnalyticsV3PreviewResponse>();
        Assert.NotNull(preview);

        using var executeResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/execute",
            new PmAnalyticsV3ExecuteRequest(preview.PreviewId));
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        var execution = await executeResponse.Content.ReadFromJsonAsync<PmAnalyticsV3ExecutionResponse>();
        Assert.NotNull(execution);
        Assert.Equal(1, execution.Population.EligibleAssetCycleCount);
        Assert.Equal(1, execution.AggregateRows[0].Measures[PmAnalyticsV3Catalog.ScheduledCount].Value);
    }

    [Fact]
    public async Task All_v3_routes_enforce_gsd_authorization()
    {
        await using var supervisor = new AnalyticsApplicationFactory(authenticated: true, AuthRoleCatalog.Supervisor);
        using var supervisorClient = supervisor.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorClient.GetAsync("/api/v1/analytics/pm/v3/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorClient.GetAsync(
            "/api/v1/analytics/pm/v3/catalog/values?field=department&categories=fire-extinguisher&search=Test")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show scheduled fire extinguishers during 2024"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/preview",
            new PmAnalyticsV3PreviewRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisorClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/execute",
            new PmAnalyticsV3ExecuteRequest("invalid"))).StatusCode);

        await using var anonymous = new AnalyticsApplicationFactory(authenticated: false);
        using var anonymousClient = anonymous.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/v1/analytics/pm/v3/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync(
            "/api/v1/analytics/pm/v3/catalog/values?field=department&categories=fire-extinguisher&search=Test")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show scheduled fire extinguishers during 2024"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/preview",
            new PmAnalyticsV3PreviewRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/execute",
            new PmAnalyticsV3ExecuteRequest("invalid"))).StatusCode);
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        var assetId = Guid.NewGuid();
        var cycle = "2024-02";
        var asset = new Asset
        {
            Id = assetId,
            AssetCode = "FE-V3-001",
            AssetCategory = "fire-extinguisher",
            Department = "Synthetic GSD",
            Building = "Synthetic Building",
            Location = "Synthetic Location",
            Status = "Active"
        };
        context.Assets.Add(asset);
        context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            Asset = asset,
            PmCycle = cycle,
            ScheduleDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Due
        });
        await context.SaveChangesAsync();
    }

    private sealed class AnalyticsApplicationFactory(bool authenticated, params string[] roles)
        : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"PmAnalyticsV3Endpoint_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false",
                    ["NaturalLanguageAnalytics:V3DeepSeekEnabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                if (authenticated) services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
