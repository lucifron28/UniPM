using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class DevelopmentUserSeederTests
{
    private const string Password = "TestOnlyPassword123!";

    [Fact]
    public async Task Seeder_creates_expected_users_is_idempotent_and_repairs_assignments()
    {
        await using var application = new AuthEndpointsTests.AuthApplicationFactory();
        await using var scope = application.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentUserSeeder>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var first = await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        Assert.Equal(5, first.RolesCreated);
        Assert.Equal(5, first.UsersCreated);
        Assert.Equal(5, first.RoleAssignmentsRepaired);
        Assert.Equal(new DevelopmentUserSeedResult(0, 0, 0, 0), second);
        Assert.All(AuthRoleCatalog.Values, role => Assert.True(roleManager.RoleExistsAsync(role).Result));
        Assert.Equal(5, userManager.Users.Count());
        Assert.DoesNotContain(Password, first.ToString(), StringComparison.Ordinal);

        var gsd = Assert.IsType<ApplicationUser>(
            await userManager.FindByEmailAsync("gsd@unipm.local"));
        Assert.True((await userManager.RemoveFromRoleAsync(gsd, AuthRoleCatalog.Gsd)).Succeeded);
        gsd.IsActive = false;
        Assert.True((await userManager.UpdateAsync(gsd)).Succeeded);

        var repaired = await seeder.SeedAsync();

        Assert.Equal(1, repaired.RoleAssignmentsRepaired);
        Assert.Equal(1, repaired.UsersReactivated);
        Assert.True(await userManager.IsInRoleAsync(gsd, AuthRoleCatalog.Gsd));
        Assert.True((await userManager.FindByEmailAsync("gsd@unipm.local"))!.IsActive);
        Assert.Equal(5, userManager.Users.Count());
    }

    [Fact]
    public async Task Seeder_refuses_to_run_outside_development()
    {
        await using var application = new AuthEndpointsTests.AuthApplicationFactory();
        await using var scope = application.Services.CreateAsyncScope();
        var seeder = new DevelopmentUserSeeder(
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<IConfiguration>(),
            new TestHostEnvironment(Environments.Production));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(seeder.SeedAsync);

        Assert.Contains("only in Development", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
