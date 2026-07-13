using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.MaintenanceReview;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task Anonymous_protected_request_returns_unauthorized_and_reads_remain_anonymous()
    {
        await using var application = new AuthEndpointsTests.AuthApplicationFactory();
        using var client = application.CreateClient();

        var write = await client.PostAsJsonAsync("/api/v1/assets/", AssetRequest("AUTH-ANON"));
        var read = await client.GetAsync("/api/v1/assets/");

        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/assets/")]
    [InlineData("/api/v1/schedules/")]
    [InlineData("/api/v1/inspections/")]
    [InlineData("/api/v1/maintenance-review")]
    public async Task Admin_only_user_is_forbidden_from_operational_endpoints(string route)
    {
        await using var application = new PolicyApplicationFactory(AuthRoleCatalog.Admin);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync(route, new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_operational_role_is_forbidden()
    {
        await using var application = new PolicyApplicationFactory(AuthRoleCatalog.Inspector);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/assets/", AssetRequest("AUTH-WRONG"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Gsd_can_manage_assets()
    {
        await using var application = new PolicyApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/assets/", AssetRequest("AUTH-GSD"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Gsd)]
    [InlineData(AuthRoleCatalog.Supervisor)]
    public async Task Gsd_and_supervisor_can_manage_schedules(string role)
    {
        await using var application = new PolicyApplicationFactory(role);
        var assetId = await application.SeedAssetAsync();
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = DateTimeOffset.UtcNow.AddDays(1),
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Gsd)]
    [InlineData(AuthRoleCatalog.Inspector)]
    public async Task Gsd_and_inspector_can_submit_inspections(string role)
    {
        await using var application = new PolicyApplicationFactory(role);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/inspections/", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Gsd)]
    [InlineData(AuthRoleCatalog.Supervisor)]
    [InlineData(AuthRoleCatalog.DepartmentHead)]
    public async Task Approved_operational_roles_can_use_maintenance_review(string role)
    {
        await using var application = new PolicyApplicationFactory(role);
        using var client = application.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/maintenance-review", new
        {
            assetId = Guid.NewGuid(),
            findingText = "low pressure",
            generateSummary = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static object AssetRequest(string code) => new
    {
        assetCode = code,
        assetCategory = "fire-extinguisher",
        building = "Test Building",
        department = "GSD",
        location = "Test Room"
    };

    private sealed class PolicyApplicationFactory(params string[] roles)
        : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-policy-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MaintenanceReview:Enabled"] = "true"
                }));
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IMaintenanceReviewService>();
                services.AddSingleton<IMaintenanceReviewService, SuccessfulReviewService>();
            });
        }

        public async Task<Guid> SeedAssetAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"AUTH-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                AssetCategory = "fire-extinguisher",
                Building = "Test Building",
                Department = "GSD",
                Location = "Test Room",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Assets.Add(asset);
            await context.SaveChangesAsync();
            return asset.Id;
        }

        public async Task<Guid> SeedScheduleAsync()
        {
            var assetId = await SeedAssetAsync();
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = assetId,
                ScheduleDate = now,
                PeriodType = "Quarter",
                Status = "Due",
                Quarter = "Q1",
                Year = 2026,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();
            return schedule.Id;
        }
    }

    private sealed class SuccessfulReviewService : IMaintenanceReviewService
    {
        public Task<MaintenanceReviewResponse> ReviewAsync(
            MaintenanceReviewRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new MaintenanceReviewResponse(
                new MaintenanceReviewAssetResponse(
                    request.AssetId,
                    "TEST-001",
                    "fire-extinguisher",
                    "Test Building",
                    "GSD",
                    "Test Room"),
                ["low_pressure"],
                "same_asset_history_found",
                false,
                new MaintenanceReviewRetrievalStatusResponse(
                    true,
                    1,
                    "success",
                    "unavailable",
                    "rrf",
                    60),
                "not_requested",
                null,
                [],
                []));
    }
}
