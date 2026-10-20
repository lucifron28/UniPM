using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    public async Task Assignment_queue_returns_complete_grouped_batches_only_for_the_authenticated_supervisor_scope()
    {
        var databaseName = $"unipm-assignment-queue-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        var supervisorId = await supervisorApplication.SeedAuthenticatedUserAsync(AuthRoleCatalog.Supervisor);
        var otherSupervisorId = await gsdApplication.SeedUserAsync("other-supervisor", AuthRoleCatalog.Supervisor);
        var inspectorId = await gsdApplication.SeedUserAsync("inspector", AuthRoleCatalog.Inspector);

        var ownedFirst = await CreateScheduleAsync(
            gsdClient,
            "CCMS",
            "Main Building",
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var ownedSecond = await CreateScheduleAsync(
            gsdClient,
            " ccms ",
            "Annex",
            new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var mixedOwner = await CreateScheduleAsync(
            gsdClient,
            "CCMS",
            "Library",
            new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherCycle = await CreateScheduleAsync(
            gsdClient,
            "CCMS",
            "Main Building",
            new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherDepartment = await CreateScheduleAsync(
            gsdClient,
            "Library",
            "Library",
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await gsdApplication.SetAssignmentsAsync([ownedFirst.Id, ownedSecond.Id], inspectorId, supervisorId);
        await gsdApplication.SetAssignmentsAsync([mixedOwner.Id], null, otherSupervisorId);
        await gsdApplication.SetAssignmentsAsync([otherCycle.Id], null, otherSupervisorId);
        await gsdApplication.SetAssignmentsAsync([otherDepartment.Id], inspectorId, otherSupervisorId);
        await gsdApplication.AddInspectionWithoutScheduleCompletionAsync(
            ownedSecond.Id,
            ownedSecond.AssetId,
            inspectorId);

        var gsdResponse = await gsdClient.GetAsync(
            "/api/v1/schedules/assignment-batches?year=2026&assetCategory=fire-extinguisher&pmCycle=2026-08");
        gsdResponse.EnsureSuccessStatusCode();
        var gsdQueue = await gsdResponse.Content.ReadFromJsonAsync<AssignmentQueueResponse>();
        Assert.NotNull(gsdQueue);
        Assert.Equal(4, gsdQueue.TotalScheduleCount);
        Assert.Equal(2, gsdQueue.Batches.Count);
        var gsdAugustBatch = Assert.Single(gsdQueue.Batches, batch =>
            batch.Department.Equals("CCMS", StringComparison.OrdinalIgnoreCase)
            && batch.PmCycle == "2026-08");
        Assert.Equal(3, gsdAugustBatch.ScheduleCount);
        Assert.Equal(new[] { "Annex", "Library", "Main Building" }, gsdAugustBatch.Buildings);
        Assert.Equal("Mixed", gsdAugustBatch.SupervisorState);
        Assert.Null(gsdAugustBatch.SupervisorUserId);
        Assert.Null(gsdAugustBatch.SupervisorDisplayName);
        Assert.False(gsdAugustBatch.SupervisorOwnershipIsConsistent);
        Assert.Equal("Partially assigned", gsdAugustBatch.InspectorState);
        Assert.Null(gsdAugustBatch.InspectorUserId);
        Assert.Null(gsdAugustBatch.InspectorDisplayName);
        Assert.True(gsdAugustBatch.AssignmentLocked);

        var libraryBatch = Assert.Single(gsdQueue.Batches, batch =>
            batch.Department.Equals("Library", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("other-supervisor user", libraryBatch.SupervisorDisplayName);
        Assert.Equal("inspector user", libraryBatch.InspectorDisplayName);

        var supervisorResponse = await supervisorClient.GetAsync(
            "/api/v1/schedules/assignment-batches?year=2026&assetCategory=fire-extinguisher&pmCycle=2026-08");
        supervisorResponse.EnsureSuccessStatusCode();
        var supervisorQueue = await supervisorResponse.Content.ReadFromJsonAsync<AssignmentQueueResponse>();
        Assert.NotNull(supervisorQueue);
        Assert.Equal(3, supervisorQueue.TotalScheduleCount);
        var supervisorAugustBatch = Assert.Single(supervisorQueue.Batches);
        Assert.Equal("2026-08", supervisorAugustBatch.PmCycle);
        Assert.Equal(3, supervisorAugustBatch.ScheduleCount);
        Assert.Equal("Mixed", supervisorAugustBatch.SupervisorState);
        Assert.False(supervisorAugustBatch.SupervisorOwnershipIsConsistent);
        Assert.True(supervisorAugustBatch.AssignmentLocked);
    }

    [Fact]
    public async Task Gsd_reassigns_supervisor_for_matching_batch_and_preserves_existing_inspector()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        await application.SeedAuthenticatedUserAsync(AuthRoleCatalog.Gsd);
        var oldWorkerId = await application.SeedUserAsync("old-worker", AuthRoleCatalog.Inspector);
        var oldSupervisorId = await application.SeedUserAsync("old-supervisor", AuthRoleCatalog.Supervisor);
        var supervisorId = await application.SeedUserAsync("new-supervisor", AuthRoleCatalog.Supervisor);

        var first = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var second = await CreateScheduleAsync(client, " gsd ", "Annex", new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        var third = await CreateScheduleAsync(client, "GSD", "Library", new DateTimeOffset(2026, 8, 16, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherDepartment = await CreateScheduleAsync(client, "Library", "Library", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var otherCycle = await CreateScheduleAsync(client, "GSD", "Main Building", new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await application.SetAssignmentsAsync(
            [first.Id, second.Id, third.Id, otherDepartment.Id, otherCycle.Id],
            oldWorkerId,
            oldSupervisorId);
        await application.SetScheduleStatusAsync(first.Id, ScheduleStatusCatalog.Completed);
        await application.AddInspectionWithoutScheduleCompletionAsync(first.Id, first.AssetId, oldWorkerId);

        var response = await client.PutAsJsonAsync($"/api/v1/schedules/{first.Id}/supervisor-reassignment", new
        {
            assigneeUserId = supervisorId,
            reason = "Previous Supervisor is unavailable"
        });

        response.EnsureSuccessStatusCode();
        var assignedBatch = await response.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignedBatch);
        Assert.Equal("GSD", assignedBatch.Department, ignoreCase: true);
        Assert.Equal("fire-extinguisher", assignedBatch.AssetCategory);
        Assert.Equal("2026-08", assignedBatch.PmCycle);
        Assert.Equal(oldWorkerId, assignedBatch.WorkerUserId);
        Assert.Equal(supervisorId, assignedBatch.SupervisorUserId);
        Assert.Equivalent(new[] { second.Id, third.Id }, assignedBatch.ScheduleIds);

        var stored = await application.GetAssignmentsAsync();
        Assert.Equal(oldWorkerId, stored[first.Id].WorkerId);
        Assert.Equal(oldSupervisorId, stored[first.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[second.Id].WorkerId);
        Assert.Equal(supervisorId, stored[second.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[third.Id].WorkerId);
        Assert.Equal(supervisorId, stored[third.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[otherDepartment.Id].WorkerId);
        Assert.Equal(oldSupervisorId, stored[otherDepartment.Id].SupervisorId);
        Assert.Equal(oldWorkerId, stored[otherCycle.Id].WorkerId);
        Assert.Equal(oldSupervisorId, stored[otherCycle.Id].SupervisorId);

        var audits = await application.GetAssignmentAuditsAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit =>
        {
            Assert.Equal("Supervisor", audit.AssignmentRole);
            Assert.Equal(oldSupervisorId, audit.PreviousAssigneeUserId);
            Assert.Equal(supervisorId, audit.NewAssigneeUserId);
            Assert.Equal(TestAuthenticationHandler.UserId, audit.ChangedByUserId);
            Assert.Equal("Previous Supervisor is unavailable", audit.Reason);
        });
        Assert.Equal(new[] { second.Id, third.Id }, audits.Select(audit => audit.ScheduleId).Order().ToArray());
        Assert.Equal(oldWorkerId, await application.GetInspectionInspectorAsync(first.Id));
    }

    [Fact]
    public async Task Initial_assignment_rejects_a_batch_that_has_started()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var supervisorId = await application.SeedUserAsync("supervisor", AuthRoleCatalog.Supervisor);
        var schedule = await CreateScheduleAsync(
            client,
            "GSD",
            "Main Building",
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await application.SetScheduleStatusAsync(schedule.Id, ScheduleStatusCatalog.Ongoing);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/schedules/{schedule.Id}/supervisor-assignment",
            new { supervisorUserId = supervisorId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Supervisor_reassigns_only_unfinished_inspections_and_wrong_supervisor_is_forbidden()
    {
        var databaseName = $"unipm-reassignment-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();
        var currentSupervisorId = await supervisorApplication.SeedAuthenticatedUserAsync(AuthRoleCatalog.Supervisor);
        var firstInspectorId = await gsdApplication.SeedUserAsync("first-inspector", AuthRoleCatalog.Inspector);
        var replacementInspectorId = await gsdApplication.SeedUserAsync("replacement-inspector", AuthRoleCatalog.Inspector);
        var completed = await CreateScheduleAsync(gsdClient, "CCMS", "Main Building", new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        var ongoing = await CreateScheduleAsync(gsdClient, "CCMS", "Annex", new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.FromHours(8)));
        await gsdApplication.SetAssignmentsAsync([completed.Id, ongoing.Id], firstInspectorId, currentSupervisorId);
        await gsdApplication.SetScheduleStatusAsync(completed.Id, ScheduleStatusCatalog.Completed);
        await gsdApplication.SetScheduleStatusAsync(ongoing.Id, ScheduleStatusCatalog.Ongoing);
        await gsdApplication.AddInspectionWithoutScheduleCompletionAsync(completed.Id, completed.AssetId, firstInspectorId);

        var forbidden = await gsdClient.PutAsJsonAsync($"/api/v1/schedules/{ongoing.Id}/inspector-reassignment", new
        {
            assigneeUserId = replacementInspectorId,
            reason = "Coverage change"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var response = await supervisorClient.PutAsJsonAsync($"/api/v1/schedules/{ongoing.Id}/inspector-reassignment", new
        {
            assigneeUserId = replacementInspectorId,
            reason = "Inspector unavailable"
        });

        response.EnsureSuccessStatusCode();
        var stored = await gsdApplication.GetAssignmentsAsync();
        Assert.Equal(firstInspectorId, stored[completed.Id].WorkerId);
        Assert.Equal(replacementInspectorId, stored[ongoing.Id].WorkerId);
        Assert.Equal(currentSupervisorId, stored[ongoing.Id].SupervisorId);
        Assert.Equal(ScheduleStatusCatalog.Completed, await gsdApplication.GetScheduleStatusAsync(completed.Id));
        Assert.Equal(firstInspectorId, await gsdApplication.GetInspectionInspectorAsync(completed.Id));
        var audit = Assert.Single(await gsdApplication.GetAssignmentAuditsAsync());
        Assert.Equal("Inspector", audit.AssignmentRole);
        Assert.Equal(firstInspectorId, audit.PreviousAssigneeUserId);
        Assert.Equal(replacementInspectorId, audit.NewAssigneeUserId);
        Assert.Equal(TestAuthenticationHandler.UserId, audit.ChangedByUserId);
        Assert.Equal("Inspector unavailable", audit.Reason);
        Assert.Equal(ongoing.Id, audit.ScheduleId);
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
        var workerIds = new List<Guid>();
        var supervisorIds = new List<Guid>();
        for (var index = 0; index < 12; index++)
        {
            workerIds.Add(await gsdApplication.SeedUserAsync(
                $"active-worker-{index:D2}", AuthRoleCatalog.Inspector));
            supervisorIds.Add(await gsdApplication.SeedUserAsync(
                $"active-supervisor-{index:D2}", AuthRoleCatalog.Supervisor));
        }

        var workerId = workerIds[^1];
        var supervisorId = supervisorIds[^1];

        var supervisorOptionsResponse = await gsdClient.GetAsync("/api/v1/schedules/supervisor-assignment-options");
        supervisorOptionsResponse.EnsureSuccessStatusCode();
        var supervisorOptions = await supervisorOptionsResponse.Content.ReadFromJsonAsync<SupervisorOptionsResponse>();
        Assert.NotNull(supervisorOptions);
        Assert.Equal(10, supervisorOptions.Supervisors.Count);
        Assert.DoesNotContain(supervisorOptions.Supervisors, option => option.Id == supervisorId);
        var searchedSupervisorsResponse = await gsdClient.GetAsync(
            $"/api/v1/schedules/supervisor-assignment-options?search=active-supervisor-11&selectedUserId={supervisorIds[10]}");
        searchedSupervisorsResponse.EnsureSuccessStatusCode();
        var searchedSupervisors = await searchedSupervisorsResponse.Content.ReadFromJsonAsync<SupervisorOptionsResponse>();
        Assert.NotNull(searchedSupervisors);
        Assert.Contains(searchedSupervisors.Supervisors, option => option.Id == supervisorId);
        Assert.Equal(supervisorIds[10], searchedSupervisors.Selected?.Id);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gsdClient.GetAsync("/api/v1/schedules/assignment-options")).StatusCode);

        var workerOptionsResponse = await supervisorClient.GetAsync("/api/v1/schedules/assignment-options");
        workerOptionsResponse.EnsureSuccessStatusCode();
        var workerOptions = await workerOptionsResponse.Content.ReadFromJsonAsync<WorkerOptionsResponse>();
        Assert.NotNull(workerOptions);
        Assert.Equal(10, workerOptions.Workers.Count);
        Assert.DoesNotContain(workerOptions.Workers, option => option.Id == workerId);
        var searchedWorkersResponse = await supervisorClient.GetAsync(
            $"/api/v1/schedules/assignment-options?search=active-worker-11&selectedUserId={workerIds[10]}");
        searchedWorkersResponse.EnsureSuccessStatusCode();
        var searchedWorkers = await searchedWorkersResponse.Content.ReadFromJsonAsync<WorkerOptionsResponse>();
        Assert.NotNull(searchedWorkers);
        Assert.Contains(searchedWorkers.Workers, option => option.Id == workerId);
        Assert.Equal(workerIds[10], searchedWorkers.Selected?.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await supervisorClient.GetAsync($"/api/v1/schedules/assignment-options?search={new string('x', 101)}")).StatusCode);
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

        var schedule = await CreateScheduleAsync(
            gsdClient,
            "CCMS",
            "Main Building",
            new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.FromHours(8)));
        await gsdApplication.SetAssignmentsAsync([schedule.Id], workerId, supervisorId);
        var queueResponse = await gsdClient.GetAsync(
            "/api/v1/schedules/assignment-batches?year=2026&department=CCMS&pmCycle=2026-08");
        queueResponse.EnsureSuccessStatusCode();
        var queue = await queueResponse.Content.ReadFromJsonAsync<AssignmentQueueResponse>();
        Assert.NotNull(queue);
        var assignedBatch = Assert.Single(queue.Batches);
        Assert.Equal("active-supervisor-11 user", assignedBatch.SupervisorDisplayName);
        Assert.Equal("active-worker-11 user", assignedBatch.InspectorDisplayName);
    }

    [Fact]
    public async Task Assignment_options_search_and_page_active_users_by_role_and_preserve_selection()
    {
        var databaseName = $"unipm-assignment-options-{Guid.NewGuid():N}";
        await using var gsdApplication = new TestApplicationFactory(AuthRoleCatalog.Gsd, databaseName);
        await using var supervisorApplication = new TestApplicationFactory(AuthRoleCatalog.Supervisor, databaseName);
        using var gsdClient = gsdApplication.CreateClient();
        using var supervisorClient = supervisorApplication.CreateClient();

        var firstInspector = await gsdApplication.SeedUserAsync(
            "searchable-inspector-a",
            AuthRoleCatalog.Inspector);
        var secondInspector = await gsdApplication.SeedUserAsync(
            "searchable-inspector-b",
            AuthRoleCatalog.Inspector);
        await gsdApplication.SeedUserAsync("other-inspector", AuthRoleCatalog.Inspector);
        await gsdApplication.SeedUserAsync("searchable-supervisor", AuthRoleCatalog.Supervisor);

        var workerResponse = await supervisorClient.GetAsync(
            $"/api/v1/schedules/assignment-options?search=searchable-inspector&page=2&pageSize=1&selectedUserId={firstInspector}");
        workerResponse.EnsureSuccessStatusCode();
        var workers = await workerResponse.Content.ReadFromJsonAsync<WorkerOptionsResponse>();

        Assert.NotNull(workers);
        Assert.Equal(2, workers.Page);
        Assert.Equal(1, workers.PageSize);
        Assert.Equal(2, workers.TotalCount);
        Assert.Equal(firstInspector, workers.Selected?.Id);
        Assert.Equal(secondInspector, Assert.Single(workers.Workers).Id);

        var supervisorResponse = await gsdClient.GetAsync(
            "/api/v1/schedules/supervisor-assignment-options?search=searchable&page=1&pageSize=5");
        supervisorResponse.EnsureSuccessStatusCode();
        var supervisors = await supervisorResponse.Content.ReadFromJsonAsync<SupervisorOptionsResponse>();

        Assert.NotNull(supervisors);
        Assert.Equal(1, supervisors.TotalCount);
        Assert.Equal("searchable-supervisor user", Assert.Single(supervisors.Supervisors).DisplayName);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await supervisorClient.GetAsync("/api/v1/schedules/assignment-options?pageSize=101")).StatusCode);
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
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
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

        public async Task<List<ScheduleAssignmentReassignmentAudit>> GetAssignmentAuditsAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.ScheduleAssignmentReassignmentAudits
                .OrderBy(audit => audit.ScheduleId)
                .ToListAsync();
        }

        public async Task<Guid> GetInspectionInspectorAsync(Guid scheduleId)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            return await context.InspectionRecords
                .Where(record => record.ScheduleId == scheduleId)
                .Select(record => record.InspectorUserId)
                .SingleAsync();
        }
    }

    private sealed record AssetRef(Guid Id);
    private sealed record ScheduleRef(Guid Id, Guid AssetId);
    private sealed record ScheduleAssignment(Guid? WorkerId, Guid? SupervisorId);
    private sealed record AssigneeOption(Guid Id, string DisplayName);
    private sealed record WorkerOptionsResponse(
        IReadOnlyList<AssigneeOption> Workers,
        int Page,
        int PageSize,
        int TotalCount,
        AssigneeOption? Selected);
    private sealed record SupervisorOptionsResponse(
        IReadOnlyList<AssigneeOption> Supervisors,
        int Page,
        int PageSize,
        int TotalCount,
        AssigneeOption? Selected);
    private sealed record AssignmentQueueResponse(
        int TotalScheduleCount,
        IReadOnlyList<AssignmentQueueBatchResponse> Batches);
    private sealed record AssignmentQueueBatchResponse(
        string BatchKey,
        string Department,
        string AssetCategory,
        string PmCycle,
        int ScheduleCount,
        int UnfinishedScheduleCount,
        int CompletedCount,
        int OngoingCount,
        int OverdueCount,
        string SupervisorState,
        Guid? SupervisorUserId,
        string? SupervisorDisplayName,
        string InspectorState,
        Guid? InspectorUserId,
        string? InspectorDisplayName,
        bool AssignmentLocked,
        bool SupervisorOwnershipIsConsistent,
        IReadOnlyList<string> Buildings,
        ScheduleResponse RepresentativeSchedule);
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
