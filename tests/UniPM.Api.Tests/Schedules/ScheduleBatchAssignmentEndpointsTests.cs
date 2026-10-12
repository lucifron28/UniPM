using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class ScheduleBatchAssignmentEndpointsTests
{
    [Fact]
    public async Task Gsd_supervisor_assignment_covers_matching_batch_and_clears_old_worker()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var oldWorkerId = await application.SeedUserAsync("old-worker", AuthRoleCatalog.Inspector);
        var oldSupervisorId = await application.SeedUserAsync("old-supervisor", AuthRoleCatalog.Supervisor);
        var supervisorId = await application.SeedUserAsync("new-supervisor", AuthRoleCatalog.Supervisor);

        var first = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var second = await CreateScheduleAsync(client, " gsd ", "Annex", new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherDepartment = await CreateScheduleAsync(client, "Library", "Library", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherCycle = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await application.SetAssignmentsAsync(
            [first.Id, second.Id, otherDepartment.Id, otherCycle.Id],
            oldWorkerId,
            oldSupervisorId);

        var response = await client.PutAsJsonAsync($"/api/v1/schedules/{first.Id}/supervisor-assignment", new
        {
            supervisorUserId = supervisorId
        });

        response.EnsureSuccessStatusCode();
        var assignedBatch = await response.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignedBatch);
        Assert.Equal("GSD", assignedBatch.Department, ignoreCase: true);
        Assert.Equal("fire-extinguisher", assignedBatch.AssetCategory);
        Assert.Equal("2026-08", assignedBatch.PmCycle);
        Assert.Null(assignedBatch.WorkerUserId);
        Assert.Null(assignedBatch.WorkerDisplayName);
        Assert.Equal(supervisorId, assignedBatch.SupervisorUserId);
        Assert.Equivalent(new[] { first.Id, second.Id }, assignedBatch.ScheduleIds);

        var stored = await application.GetAssignmentsAsync();
        Assert.Null(stored[first.Id].WorkerId);
        Assert.Equal(supervisorId, stored[first.Id].SupervisorId);
        Assert.Null(stored[second.Id].WorkerId);
        Assert.Equal(supervisorId, stored[second.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[otherDepartment.Id].WorkerId);
        Assert.Equal(oldSupervisorId, stored[otherDepartment.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[otherCycle.Id].WorkerId);
        Assert.Equal(oldSupervisorId, stored[otherCycle.Id].SupervisorId);
    }

    [Fact]
    public async Task Supervisor_assigns_worker_only_to_a_batch_owned_by_authenticated_supervisor()
    {
        var databaseName = $"unipm-assignment-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        var supervisorId = await supervisorApplication.SeedAuthenticatedUserAsync(AuthRoleCatalog.Supervisor);
        var workerId = await gsdApplication.SeedUserAsync("worker", AuthRoleCatalog.Inspector);
        var first = await CreateScheduleAsync(gsdClient, "CCMS", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var second = await CreateScheduleAsync(gsdClient, "CCMS", "Annex", new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var supervisorAssignment = await gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{first.Id}/supervisor-assignment",
            new { supervisorUserId = supervisorId });
        supervisorAssignment.EnsureSuccessStatusCode();

        var response = await supervisorClient.PutAsJsonAsync($"/api/v1/schedules/{first.Id}/assignment", new
        {
            workerUserId = workerId
        });

        response.EnsureSuccessStatusCode();
        var assignedBatch = await response.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignedBatch);
        Assert.Equal(workerId, assignedBatch.WorkerUserId);
        Assert.Equal(supervisorId, assignedBatch.SupervisorUserId);
        Assert.Equivalent(new[] { first.Id, second.Id }, assignedBatch.ScheduleIds);
        var repeatedSupervisorAssignment = await gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{first.Id}/supervisor-assignment",
            new { supervisorUserId = supervisorId });
        repeatedSupervisorAssignment.EnsureSuccessStatusCode();
        var preservedAssignment = await repeatedSupervisorAssignment.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(preservedAssignment);
        Assert.Equal(workerId, preservedAssignment.WorkerUserId);
        var stored = await supervisorApplication.GetAssignmentsAsync();
        Assert.Equal(workerId, stored[first.Id].WorkerId);
        Assert.Equal(supervisorId, stored[first.Id].SupervisorId);
        Assert.Equal(workerId, stored[second.Id].WorkerId);
        Assert.Equal(supervisorId, stored[second.Id].SupervisorId);
    }

    [Fact]
    public async Task Assignment_options_and_mutations_are_limited_to_the_relevant_role()
    {
        var databaseName = $"unipm-assignment-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        using var inspectorClient = inspectorApplication.CreateClient();
        var workerId = await gsdApplication.SeedUserAsync("active-worker", AuthRoleCatalog.Inspector);
        var supervisorId = await gsdApplication.SeedUserAsync("active-supervisor", AuthRoleCatalog.Supervisor);

        var supervisorOptionsResponse = await gsdClient.GetAsync("/api/v1/schedules/supervisor-assignment-options");
        supervisorOptionsResponse.EnsureSuccessStatusCode();
        var supervisorOptions = await supervisorOptionsResponse.Content.ReadFromJsonAsync<SupervisorOptionsResponse>();
        Assert.NotNull(supervisorOptions);
        Assert.Contains(supervisorOptions.Supervisors, option => option.Id == supervisorId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gsdClient.GetAsync("/api/v1/schedules/assignment-options")).StatusCode);

        var workerOptionsResponse = await supervisorClient.GetAsync("/api/v1/schedules/assignment-options");
        workerOptionsResponse.EnsureSuccessStatusCode();
        var workerOptions = await workerOptionsResponse.Content.ReadFromJsonAsync<WorkerOptionsResponse>();
        Assert.NotNull(workerOptions);
        Assert.Contains(workerOptions.Workers, option => option.Id == workerId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await supervisorClient.GetAsync("/api/v1/schedules/supervisor-assignment-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await inspectorClient.GetAsync("/api/v1/schedules/assignment-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await inspectorClient.PutAsJsonAsync(
                $"/api/v1/schedules/{Guid.NewGuid()}/assignment",
                new { workerUserId = workerId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await supervisorClient.PutAsJsonAsync(
                $"/api/v1/schedules/{Guid.NewGuid()}/supervisor-assignment",
                new { supervisorUserId = supervisorId })).StatusCode);
    }

    [Fact]
    public async Task Supervisor_cannot_assign_worker_to_another_supervisors_batch()
    {
        var databaseName = $"unipm-assignment-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        var authenticatedSupervisorId = await supervisorApplication.SeedAuthenticatedUserAsync(AuthRoleCatalog.Supervisor);
        var otherSupervisorId = await gsdApplication.SeedUserAsync("other-supervisor", AuthRoleCatalog.Supervisor);
        var workerId = await gsdApplication.SeedUserAsync("worker", AuthRoleCatalog.Inspector);
        var firstSchedule = await CreateScheduleAsync(gsdClient, "GSD", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var secondSchedule = await CreateScheduleAsync(gsdClient, "GSD", "Annex", new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var assignment = await gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{firstSchedule.Id}/supervisor-assignment",
            new { supervisorUserId = authenticatedSupervisorId });
        assignment.EnsureSuccessStatusCode();
        await gsdApplication.SetAssignmentsAsync([secondSchedule.Id], null, otherSupervisorId);

        var response = await supervisorClient.PutAsJsonAsync($"/api/v1/schedules/{firstSchedule.Id}/assignment", new
        {
            workerUserId = workerId
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await supervisorApplication.GetAssignmentsAsync();
        Assert.Null(stored[firstSchedule.Id].WorkerId);
        Assert.Equal(authenticatedSupervisorId, stored[firstSchedule.Id].SupervisorId);
        Assert.Null(stored[secondSchedule.Id].WorkerId);
        Assert.Equal(otherSupervisorId, stored[secondSchedule.Id].SupervisorId);
        Assert.NotEqual(authenticatedSupervisorId, otherSupervisorId);
    }

    [Fact]
    public async Task Linked_inspection_blocks_both_assignment_stages_even_when_schedule_is_overdue()
    {
        var databaseName = $"unipm-assignment-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        var supervisorId = await supervisorApplication.SeedAuthenticatedUserAsync(AuthRoleCatalog.Supervisor);
        var workerId = await gsdApplication.SeedUserAsync("worker", AuthRoleCatalog.Inspector);
        var replacementSupervisorId = await gsdApplication.SeedUserAsync("replacement-supervisor", AuthRoleCatalog.Supervisor);
        var schedule = await CreateScheduleAsync(gsdClient, "GSD", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var supervisorAssignment = await gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{schedule.Id}/supervisor-assignment",
            new { supervisorUserId = supervisorId });
        supervisorAssignment.EnsureSuccessStatusCode();
        await gsdApplication.AddInspectionWithoutScheduleCompletionAsync(schedule.Id, schedule.AssetId, workerId);

        var gsdResponse = await gsdClient.PutAsJsonAsync(
            $"/api/v1/schedules/{schedule.Id}/supervisor-assignment",
            new { supervisorUserId = replacementSupervisorId });
        var supervisorResponse = await supervisorClient.PutAsJsonAsync(
            $"/api/v1/schedules/{schedule.Id}/assignment",
            new { workerUserId = workerId });

        Assert.Equal(HttpStatusCode.Conflict, gsdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, supervisorResponse.StatusCode);
        var stored = await supervisorApplication.GetAssignmentsAsync();
        Assert.Null(stored[schedule.Id].WorkerId);
        Assert.Equal(supervisorId, stored[schedule.Id].SupervisorId);
        Assert.Equal(ScheduleStatusCatalog.Overdue, await supervisorApplication.GetScheduleStatusAsync(schedule.Id));
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    public async Task Gsd_cannot_reassign_a_completed_or_cancelled_schedule(string status)
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var oldSupervisorId = await application.SeedUserAsync("old-supervisor", AuthRoleCatalog.Supervisor);
        var newSupervisorId = await application.SeedUserAsync("new-supervisor", AuthRoleCatalog.Supervisor);
        var schedule = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await application.SetAssignmentsAsync([schedule.Id], null, oldSupervisorId);
        await application.SetScheduleStatusAsync(schedule.Id, status);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/schedules/{schedule.Id}/supervisor-assignment",
            new { supervisorUserId = newSupervisorId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await application.GetAssignmentsAsync();
        Assert.Equal(oldSupervisorId, stored[schedule.Id].SupervisorId);
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

    private sealed class TestApplicationFactory(string role, string? sharedDatabaseName = null) : WebApplicationFactory<Program>
    {
        private static readonly ConcurrentDictionary<string, InMemoryDatabaseRoot> DatabaseRoots = new();
        private readonly string databaseName = sharedDatabaseName ?? $"unipm-assignment-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(role);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(
                        databaseName,
                        DatabaseRoots.GetOrAdd(databaseName, _ => new InMemoryDatabaseRoot())));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                    new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.FromHours(8))));
            });
        }

        public Task<Guid> SeedUserAsync(string suffix, string role)
        {
            return SeedUserAsync(Guid.NewGuid(), suffix, role);
        }

        public Task<Guid> SeedAuthenticatedUserAsync(string role)
        {
            return SeedUserAsync(TestAuthenticationHandler.UserId, "authenticated-supervisor", role);
        }

        private async Task<Guid> SeedUserAsync(Guid id, string suffix, string role)
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
                Id = id,
                UserName = $"{suffix}-{id:N}@unipm.local",
                Email = $"{suffix}-{id:N}@unipm.local",
                EmailConfirmed = true,
                DisplayName = $"{suffix} user",
                IsActive = true
            };
            Assert.True((await userManager.CreateAsync(user, "Demo-Account-123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, role)).Succeeded);
            return user.Id;
        }

        public async Task SetAssignmentsAsync(
            IReadOnlyCollection<Guid> scheduleIds,
            Guid? workerId,
            Guid? supervisorId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var schedules = await context.PreventiveMaintenanceSchedules
                .Where(schedule => scheduleIds.Contains(schedule.Id))
                .ToListAsync();
            foreach (var schedule in schedules)
            {
                schedule.AssignedToUserId = workerId;
                schedule.AssignedSupervisorUserId = supervisorId;
            }

            await context.SaveChangesAsync();
        }

        public async Task SetScheduleStatusAsync(Guid scheduleId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules.SingleAsync(item => item.Id == scheduleId);
            schedule.Status = status;
            if (status == ScheduleStatusCatalog.Completed)
            {
                schedule.CompletedAt = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync();
        }

        public async Task AddInspectionWithoutScheduleCompletionAsync(
            Guid scheduleId,
            Guid assetId,
            Guid inspectorUserId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            context.InspectionRecords.Add(new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = scheduleId,
                AssetId = assetId,
                InspectorUserId = inspectorUserId,
                DateInspected = now,
                IsOperational = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }

        public async Task<string> GetScheduleStatusAsync(Guid scheduleId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.PreventiveMaintenanceSchedules
                .Where(schedule => schedule.Id == scheduleId)
                .Select(schedule => schedule.Status)
                .SingleAsync();
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
    private sealed record ScheduleRef(Guid Id, Guid AssetId);
    private sealed record ScheduleAssignment(Guid? WorkerId, Guid? SupervisorId);
    private sealed record AssigneeOption(Guid Id, string DisplayName);
    private sealed record WorkerOptionsResponse(IReadOnlyList<AssigneeOption> Workers);
    private sealed record SupervisorOptionsResponse(IReadOnlyList<AssigneeOption> Supervisors);
    private sealed record AssignmentResponse(
        string Department,
        string AssetCategory,
        string PmCycle,
        Guid? WorkerUserId,
        string? WorkerDisplayName,
        Guid SupervisorUserId,
        string SupervisorDisplayName,
        IReadOnlyList<Guid> ScheduleIds);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
