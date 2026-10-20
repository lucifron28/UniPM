using Microsoft.AspNetCore.Identity;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
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
    IHostEnvironment environment,
    ApplicationDbContext? context = null)
{
    private const string SkipDepartmentHeadAccountSetting = "UNIPM_DEV_SKIP_DEPARTMENT_HEAD_ACCOUNT";
    private const string DepartmentHeadEmail = "departmenthead@unipm.local";

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

        var staging = StagingDemoInitialization.Enabled(configuration);
        var passwords = staging ? StagingDemoInitialization.Passwords(configuration) : null;
        var password = staging ? null : configuration["UNIPM_DEV_USER_PASSWORD"];
        if (!staging && string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "UNIPM_DEV_USER_PASSWORD must be configured for Development user seeding.");
        }

        var skipDepartmentHeadSetting = configuration[SkipDepartmentHeadAccountSetting];
        var skipDepartmentHeadAccount = false;
        if (skipDepartmentHeadSetting is not null
            && !bool.TryParse(skipDepartmentHeadSetting, out skipDepartmentHeadAccount))
        {
            throw new InvalidOperationException(
                $"{SkipDepartmentHeadAccountSetting} must be true or false.");
        }

        var definitions = Users.Where(user =>
            staging ? StagingDemoInitialization.Roles.Contains(user.Role)
                : !skipDepartmentHeadAccount || user.Email != DepartmentHeadEmail)
            .Select(user => staging ? user with { Email = StagingDemoInitialization.Email(user.Role) } : user)
            .ToArray();
        if (staging)
        {
            // Validate every password against Identity before the first role/user write.
            foreach (var definition in definitions)
            foreach (var validator in userManager.PasswordValidators)
                EnsureSucceeded(await validator.ValidateAsync(userManager,
                    new ApplicationUser { UserName = definition.Email, Email = definition.Email },
                    passwords![definition.Role]), "validate staging credential policy");
            await StagingDemoInitialization.EnsureReadyAsync(context
                ?? throw new InvalidOperationException("Staging initialization context is unavailable."), false);
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
        foreach (var definition in definitions)
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
                    await userManager.CreateAsync(user, staging ? passwords![definition.Role] : password!),
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
