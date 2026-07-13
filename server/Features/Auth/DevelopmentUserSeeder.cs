using Microsoft.AspNetCore.Identity;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal sealed record DevelopmentUserSeedResult(
    int RolesCreated,
    int UsersCreated,
    int RoleAssignmentsRepaired,
    int UsersReactivated);

internal sealed class DevelopmentUserSeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    private static readonly DevelopmentUserDefinition[] Users =
    [
        new("admin@unipm.local", "UniPM Administrator", AuthRoleCatalog.Admin),
        new("gsd@unipm.local", "GSD Personnel", AuthRoleCatalog.Gsd),
        new("inspector@unipm.local", "Maintenance Inspector", AuthRoleCatalog.Inspector),
        new("supervisor@unipm.local", "Maintenance Supervisor", AuthRoleCatalog.Supervisor),
        new("departmenthead@unipm.local", "Department Head", AuthRoleCatalog.DepartmentHead)
    ];

    public async Task<DevelopmentUserSeedResult> SeedAsync()
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Development user seeding is available only in Development.");
        }

        var password = configuration["UNIPM_DEV_USER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "UNIPM_DEV_USER_PASSWORD must be configured for Development user seeding.");
        }

        var rolesCreated = 0;
        foreach (var roleName in AuthRoleCatalog.Values)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            EnsureSucceeded(
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)),
                $"create Development role '{roleName}'");
            rolesCreated++;
        }

        var usersCreated = 0;
        var assignmentsRepaired = 0;
        var usersReactivated = 0;
        foreach (var definition in Users)
        {
            var user = await userManager.FindByEmailAsync(definition.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = definition.Email,
                    Email = definition.Email,
                    EmailConfirmed = true,
                    DisplayName = definition.DisplayName,
                    IsActive = true
                };
                EnsureSucceeded(
                    await userManager.CreateAsync(user, password),
                    $"create Development user '{definition.Email}'");
                usersCreated++;
            }
            else
            {
                var needsUpdate = false;
                if (!user.IsActive)
                {
                    user.IsActive = true;
                    usersReactivated++;
                    needsUpdate = true;
                }

                if (!string.Equals(user.DisplayName, definition.DisplayName, StringComparison.Ordinal))
                {
                    user.DisplayName = definition.DisplayName;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    EnsureSucceeded(
                        await userManager.UpdateAsync(user),
                        $"update Development user '{definition.Email}'");
                }
            }

            if (!await userManager.IsInRoleAsync(user, definition.Role))
            {
                EnsureSucceeded(
                    await userManager.AddToRoleAsync(user, definition.Role),
                    $"assign Development role '{definition.Role}'");
                assignmentsRepaired++;
            }
        }

        return new DevelopmentUserSeedResult(
            rolesCreated,
            usersCreated,
            assignmentsRepaired,
            usersReactivated);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Code))}");
    }

    private sealed record DevelopmentUserDefinition(
        string Email,
        string DisplayName,
        string Role);
}
