using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class ScheduleBatchAssignmentEndpointsTests
{
    [Fact]
    public async Task Gsd_assignment_applies_to_only_the_matching_department_category_and_cycle()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var workerId = await application.SeedUserAsync("worker", AuthRoleCatalog.Inspector);
        var supervisorId = await application.SeedUserAsync("supervisor", AuthRoleCatalog.Supervisor);

        var first = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var second = await CreateScheduleAsync(client, " gsd ", "Annex", new DateTimeOffset(2026, 9, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherDepartment = await CreateScheduleAsync(client, "Library", "Library", new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherCycle = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(8)));

        var response = await client.PutAsJsonAsync($"/api/v1/schedules/{first.Id}/assignment", new
        {
            workerUserId = workerId,
            supervisorUserId = supervisorId
        });

        response.EnsureSuccessStatusCode();
        var assignedBatch = await response.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignedBatch);
        Assert.Equal("GSD", assignedBatch.Department, ignoreCase: true);
        Assert.Equal("fire-extinguisher", assignedBatch.AssetCategory);
        Assert.Equal("2026-09", assignedBatch.PmCycle);
        Assert.Equal(workerId, assignedBatch.WorkerUserId);
        Assert.Equal(supervisorId, assignedBatch.SupervisorUserId);
        Assert.Equivalent(new[] { first.Id, second.Id }, assignedBatch.ScheduleIds);

        var stored = await application.GetAssignmentsAsync();
        Assert.Equal(workerId, stored[first.Id].WorkerId);
        Assert.Equal(supervisorId, stored[first.Id].SupervisorId);
        Assert.Equal(workerId, stored[second.Id].WorkerId);
        Assert.Equal(supervisorId, stored[second.Id].SupervisorId);
        Assert.Null(stored[otherDepartment.Id].WorkerId);
        Assert.Null(stored[otherCycle.Id].WorkerId);
    }

    [Fact]
    public async Task Assignment_options_are_available_to_gsd_but_not_inspectors()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var gsdClient = application.CreateClient();
        var workerId = await application.SeedUserAsync("active-worker", AuthRoleCatalog.Inspector);
        var supervisorId = await application.SeedUserAsync("active-supervisor", AuthRoleCatalog.Supervisor);

        var response = await gsdClient.GetAsync("/api/v1/schedules/assignment-options");
        response.EnsureSuccessStatusCode();
        var options = await response.Content.ReadFromJsonAsync<AssignmentOptionsResponse>();
        Assert.NotNull(options);
        Assert.Contains(options.Workers, option => option.Id == workerId);
        Assert.Contains(options.Supervisors, option => option.Id == supervisorId);

        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var inspectorClient = inspectorApplication.CreateClient();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await inspectorClient.GetAsync("/api/v1/schedules/assignment-options")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await inspectorClient.PutAsJsonAsync(
                $"/api/v1/schedules/{Guid.NewGuid()}/assignment",
                new { workerUserId = workerId, supervisorUserId = supervisorId })).StatusCode);
    }

    private static async Task<ScheduleRef> CreateScheduleAsync(
        HttpClient client,
        string department,
        string building,
        DateTimeOffset date)
    {
        var code = $"ASSIGN-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var assetResponse = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = code,
            assetCategory = "fire-extinguisher",
            building,
            department,
            location = "Ground Floor"
        });
        assetResponse.EnsureSuccessStatusCode();
        var asset = await assetResponse.Content.ReadFromJsonAsync<AssetRef>();
        Assert.NotNull(asset);

        var scheduleResponse = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = asset.Id,
            scheduleDate = date,
            periodType = "Custom"
        });
        scheduleResponse.EnsureSuccessStatusCode();
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<ScheduleRef>();
        Assert.NotNull(schedule);
        return schedule;
    }

    private sealed class TestApplicationFactory(string role) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-assignment-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(role);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task<Guid> SeedUserAsync(string suffix, string role)
        {
            await using var scope = Services.CreateAsyncScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync(role))
            {
                Assert.True((await roleManager.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"{suffix}@unipm.local",
                Email = $"{suffix}@unipm.local",
                EmailConfirmed = true,
                DisplayName = $"{suffix} user",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(user, "Demo-Account-123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
            return user.Id;
        }

        public async Task<Dictionary<Guid, ScheduleAssignment>> GetAssignmentsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.PreventiveMaintenanceSchedules
                .ToDictionaryAsync(
                    schedule => schedule.Id,
                    schedule => new ScheduleAssignment(schedule.AssignedToUserId, schedule.AssignedSupervisorUserId));
        }
    }

    private sealed record AssetRef(Guid Id);
    private sealed record ScheduleRef(Guid Id);
    private sealed record ScheduleAssignment(Guid? WorkerId, Guid? SupervisorId);
    private sealed record AssignmentOption(Guid Id, string DisplayName);
    private sealed record AssignmentOptionsResponse(
        IReadOnlyList<AssignmentOption> Workers,
        IReadOnlyList<AssignmentOption> Supervisors);
    private sealed record AssignmentResponse(
        string Department,
        string AssetCategory,
        string PmCycle,
        Guid WorkerUserId,
        string WorkerDisplayName,
        Guid SupervisorUserId,
        string SupervisorDisplayName,
        IReadOnlyList<Guid> ScheduleIds);
}
