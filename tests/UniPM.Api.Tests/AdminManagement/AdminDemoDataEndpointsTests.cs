using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.AdminManagement;

public sealed class AdminDemoDataEndpointsTests
{
    [Fact]
    public async Task Load_requires_an_empty_dataset_and_reset_reseeds_it_atomically()
    {
        await using var application = new AdminApplicationFactory(AuthRoleCatalog.Admin);
        await SeedManagedAccountsAsync(application);
        using var client = application.CreateClient();

        var loadResponse = await client.PostAsync("/api/v1/admin/demo-data/validation/load", null);

        Assert.Equal(HttpStatusCode.OK, loadResponse.StatusCode);
        var loaded = await loadResponse.Content.ReadFromJsonAsync<ValidationDatasetResponse>();
        Assert.NotNull(loaded);
        Assert.Equal(248, loaded.AssetsSeeded);
        Assert.Equal(2_232, loaded.SchedulesSeeded);
        Assert.Equal(248, await CountAssetsAsync(application));

        var secondLoad = await client.PostAsync("/api/v1/admin/demo-data/validation/load", null);

        Assert.Equal(HttpStatusCode.Conflict, secondLoad.StatusCode);
        Assert.Equal(248, await CountAssetsAsync(application));

        var resetResponse = await client.PostAsync("/api/v1/admin/demo-data/validation/reset", null);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var reset = await resetResponse.Content.ReadFromJsonAsync<ValidationDatasetResponse>();
        Assert.NotNull(reset);
        Assert.Equal(248, reset.Deleted.AssetsRemoved);
        Assert.Equal(2_232, reset.Deleted.SchedulesRemoved);
        Assert.Equal(248, reset.AssetsSeeded);
        Assert.Equal(248, await CountAssetsAsync(application));
    }

    [Fact]
    public async Task Validation_dataset_routes_are_admin_only()
    {
        await using var application = new AdminApplicationFactory(AuthRoleCatalog.Supervisor);
        using var client = application.CreateClient();

        var response = await client.PostAsync("/api/v1/admin/demo-data/validation/load", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedManagedAccountsAsync(AdminApplicationFactory application)
    {
        await using var context = await application.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        var roles = new[] { AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Supervisor }
            .Select(name => new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = name,
                NormalizedName = name.ToUpperInvariant()
            })
            .ToArray();
        context.Roles.AddRange(roles);
        var users = new[] { AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Supervisor }
            .Select(role => new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"{role.ToLowerInvariant()}@tests.invalid",
                NormalizedUserName = $"{role.ToUpperInvariant()}@TESTS.INVALID",
                Email = $"{role.ToLowerInvariant()}@tests.invalid",
                NormalizedEmail = $"{role.ToUpperInvariant()}@TESTS.INVALID",
                EmailConfirmed = true,
                DisplayName = $"Fictional {role}",
                IsActive = true
            })
            .ToArray();
        context.Users.AddRange(users);
        context.UserRoles.AddRange(users.Select((user, index) => new IdentityUserRole<Guid>
        {
            UserId = user.Id,
            RoleId = roles[index].Id
        }));
        await context.SaveChangesAsync();
    }

    private static async Task<int> CountAssetsAsync(AdminApplicationFactory application)
    {
        await using var context = await application.Services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();
        return await context.Assets.CountAsync();
    }

    private sealed class AdminApplicationFactory(params string[] roles) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-admin-demo-{Guid.NewGuid():N}";

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
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }
    }

    private sealed record DeleteResponse(int AssetsRemoved, int SchedulesRemoved);
    private sealed record ValidationDatasetResponse(
        DeleteResponse Deleted,
        int AssetsSeeded,
        int SchedulesSeeded,
        int InspectionsSeeded,
        int FormsSeeded,
        int AcknowledgementsSeeded,
        int WmsReferralsSeeded,
        int WmsReferralAuditsSeeded);
}
