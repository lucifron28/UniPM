using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.AdminManagement;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.AdminManagement;

public sealed class AdminAccountEndpointsTests
{
    [Fact]
    public async Task Create_update_and_password_reset_manage_identity_without_returning_passwords()
    {
        await using var application = new AdminAccountApplicationFactory();
        await application.EnsureReadyAsync();
        using var client = application.CreateClient();

        var created = await client.PostAsJsonAsync("/api/v1/admin/accounts", new
        {
            displayName = "Fictional Inspector",
            email = "inspector-one@tests.invalid",
            role = AuthRoleCatalog.Inspector,
            initialPassword = "Admin-Create-246!",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", createdJson, StringComparison.OrdinalIgnoreCase);
        var account = await created.Content.ReadFromJsonAsync<AdminAccountResponse>();
        Assert.NotNull(account);
        var originalStamp = await application.GetSecurityStampAsync(account.Id);
        await application.AddRefreshSessionAsync(account.Id, "create-update-session");

        var updated = await client.PutAsJsonAsync($"/api/v1/admin/accounts/{account.Id}", new
        {
            displayName = "Updated Inspector",
            email = "inspector-updated@tests.invalid",
            role = AuthRoleCatalog.Supervisor,
            isActive = false
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var updatedAccount = await updated.Content.ReadFromJsonAsync<AdminAccountResponse>();
        Assert.NotNull(updatedAccount);
        Assert.Equal(AuthRoleCatalog.Supervisor, updatedAccount.Role);
        Assert.False(updatedAccount.IsActive);
        Assert.NotEqual(originalStamp, await application.GetSecurityStampAsync(account.Id));
        Assert.True(await application.HasSessionRevocationReasonAsync(account.Id, "Account changed"));

        await application.AddRefreshSessionAsync(account.Id, "reset-password-session");
        var reset = await client.PostAsJsonAsync($"/api/v1/admin/accounts/{account.Id}/reset-password", new
        {
            newPassword = "Admin-Reset-357!"
        });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(await application.CheckPasswordAsync(account.Id, "Admin-Reset-357!"));
        Assert.False(await application.CheckPasswordAsync(account.Id, "Admin-Create-246!"));
        Assert.True(await application.HasSessionRevocationReasonAsync(account.Id, "Password reset by Admin"));
    }

    [Fact]
    public async Task Delete_clears_assignments_and_retains_history_referenced_accounts()
    {
        await using var application = new AdminAccountApplicationFactory();
        await application.EnsureReadyAsync();
        using var client = application.CreateClient();
        var historical = await CreateAccountAsync(client, "history@tests.invalid", AuthRoleCatalog.Inspector);
        var disposable = await CreateAccountAsync(client, "disposable@tests.invalid", AuthRoleCatalog.Supervisor);
        await application.AddHistoricalReferenceAndAssignmentAsync(historical.Id);
        await application.AddRefreshSessionAsync(historical.Id, "historical-delete-session");

        var retainedResponse = await client.DeleteAsync($"/api/v1/admin/accounts/{historical.Id}");
        var retained = await retainedResponse.Content.ReadFromJsonAsync<AdminAccountDeleteResponse>();
        var deletedResponse = await client.DeleteAsync($"/api/v1/admin/accounts/{disposable.Id}");
        var deleted = await deletedResponse.Content.ReadFromJsonAsync<AdminAccountDeleteResponse>();

        Assert.Equal(HttpStatusCode.OK, retainedResponse.StatusCode);
        Assert.NotNull(retained);
        Assert.True(retained.RetainedForHistory);
        Assert.Equal(1, retained.CurrentAssignmentsCleared);
        Assert.False(await application.IsUserActiveAsync(historical.Id));
        Assert.True(await application.HasSessionRevocationReasonAsync(historical.Id, "Account deleted"));
        Assert.Equal(HttpStatusCode.OK, deletedResponse.StatusCode);
        Assert.NotNull(deleted);
        Assert.False(deleted.RetainedForHistory);
        Assert.False(await application.UserExistsAsync(disposable.Id));
    }

    [Fact]
    public async Task Gsd_head_transfer_leaves_one_active_head_and_revokes_sessions()
    {
        await using var application = new AdminAccountApplicationFactory();
        await application.EnsureReadyAsync();
        var oldHeadId = await application.CreateUserAsync(
            Guid.NewGuid(),
            "head@tests.invalid",
            "Current GSD Head",
            AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var replacement = await CreateAccountAsync(client, "replacement@tests.invalid", AuthRoleCatalog.Supervisor);
        await application.AddRefreshSessionAsync(oldHeadId, "gsd-old-head-session");
        await application.AddRefreshSessionAsync(replacement.Id, "gsd-new-head-session");

        var transfer = await client.PutAsJsonAsync("/api/v1/admin/accounts/gsd-head", new
        {
            replacementUserId = replacement.Id
        });
        var response = await transfer.Content.ReadFromJsonAsync<AdminGsdHeadResponse>();
        var headResponse = await client.GetFromJsonAsync<AdminGsdHeadResponse>("/api/v1/admin/accounts/gsd-head");

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.NotNull(response);
        Assert.False(response.NeedsResolution);
        Assert.Equal(replacement.Id, response.ActiveHead?.Id);
        Assert.NotNull(headResponse);
        Assert.False(headResponse.NeedsResolution);
        Assert.Equal(replacement.Id, headResponse.ActiveHead?.Id);
        Assert.True(await application.HasSessionRevocationReasonAsync(oldHeadId, "GSD head changed"));
        Assert.True(await application.HasSessionRevocationReasonAsync(replacement.Id, "GSD head changed"));
    }

    [Fact]
    public async Task Non_admin_cannot_manage_accounts()
    {
        await using var application = new AdminAccountApplicationFactory(AuthRoleCatalog.Supervisor);
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/accounts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<AdminAccountResponse> CreateAccountAsync(
        HttpClient client,
        string email,
        string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/accounts", new
        {
            displayName = "Fictional Managed User",
            email,
            role,
            initialPassword = "Admin-Create-246!",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminAccountResponse>())!;
    }

    private sealed class AdminAccountApplicationFactory(params string[] roles) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-admin-accounts-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles.Length == 0 ? [AuthRoleCatalog.Admin] : roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task EnsureReadyAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (var role in new[] { AuthRoleCatalog.Admin, AuthRoleCatalog.Gsd, AuthRoleCatalog.Inspector, AuthRoleCatalog.Supervisor })
                if (!await roleManager.RoleExistsAsync(role))
                    Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            if (await roleManager.FindByNameAsync(AuthRoleCatalog.Admin) is not null
                && await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                    .FindByIdAsync(TestAuthenticationHandler.UserId.ToString()) is null)
            {
                await CreateUserAsync(
                    TestAuthenticationHandler.UserId,
                    "admin@tests.invalid",
                    "Fictional Admin",
                    AuthRoleCatalog.Admin);
            }
        }

        public async Task<Guid> CreateUserAsync(Guid id, string email, string displayName, string role)
        {
            await using var scope = Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                IsActive = true
            };
            Assert.True((await users.CreateAsync(user, "Admin-Seed-468!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            return id;
        }

        public async Task AddRefreshSessionAsync(Guid userId, string seed)
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            context.RefreshSessions.Add(new RefreshSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = $"{seed}-{Guid.NewGuid():N}",
                TokenFamilyId = Guid.NewGuid(),
                SecurityStampHash = "test-stamp",
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(1)
            });
            await context.SaveChangesAsync();
        }

        public async Task AddHistoricalReferenceAndAssignmentAsync(Guid userId)
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"FE-HISTORY-{Guid.NewGuid():N}"[..24],
                AssetCategory = "fire-extinguisher",
                Building = "Fictional Building",
                Department = "GSD",
                Location = "Test Area",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = now,
                PmCycle = $"{now.Year}-01",
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = now.Year,
                Status = "Due",
                AssignedToUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.PreventiveMaintenanceForms.Add(new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = "fire-extinguisher",
                Building = "Fictional Building",
                Department = "GSD",
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = now.Year,
                Status = "Draft",
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        public async Task<string?> GetSecurityStampAsync(Guid userId)
        {
            await using var scope = Services.CreateAsyncScope();
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(userId.ToString());
            return user?.SecurityStamp;
        }

        public async Task<bool> CheckPasswordAsync(Guid userId, string password)
        {
            await using var scope = Services.CreateAsyncScope();
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(userId.ToString());
            return user is not null && await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CheckPasswordAsync(user, password);
        }

        public async Task<bool> HasSessionRevocationReasonAsync(Guid userId, string reason)
        {
            await using var context = await Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            return await context.RefreshSessions.AnyAsync(session => session.UserId == userId
                && session.RevocationReason == reason);
        }

        public async Task<bool> IsUserActiveAsync(Guid userId)
        {
            await using var scope = Services.CreateAsyncScope();
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(userId.ToString());
            return user?.IsActive == true;
        }

        public async Task<bool> UserExistsAsync(Guid userId)
        {
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(userId.ToString()) is not null;
        }
    }
}
