using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PreventiveMaintenanceFormDraftEndpointsTests
{
    private const string TestPngSignatureBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=";

    [Fact]
    public async Task Create_draft_form_can_contain_multiple_inspection_rows()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-001", "fire-extinguisher");
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 2);

        var form = await CreateFormAsync(client, asset.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, "First draft row");
        await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second draft row");

        var response = await client.GetAsync($"/api/v1/preventive-maintenance-forms/{form.Id}");

        response.EnsureSuccessStatusCode();
        var persisted = await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>();
        Assert.NotNull(persisted);
        Assert.Equal("Draft", persisted.Status);
        Assert.Equal(2, persisted.Inspections.Count);
        Assert.Equivalent(
            new[] { firstSchedule.Id, secondSchedule.Id },
            persisted.Inspections.Select(row => row.ScheduleId));
    }

    [Fact]
    public async Task Draft_rows_reject_duplicate_or_category_mismatched_schedules()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var fireExtinguisher = await CreateAssetAsync(client, "FE-FORM-002", "fire-extinguisher");
        var fireAlarm = await CreateAssetAsync(client, "FA-FORM-001", "fire-alarm");
        var fireExtinguisherSchedule = await CreateScheduleAsync(client, fireExtinguisher.Id, 1);
        var fireAlarmSchedule = await CreateScheduleAsync(client, fireAlarm.Id, 1);
        var form = await CreateFormAsync(client, fireExtinguisher.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, fireExtinguisherSchedule.Id, "Draft pressure check");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireExtinguisherSchedule.Id, "Duplicate"));
        var categoryMismatch = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireAlarmSchedule.Id, "Wrong category"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categoryMismatch.StatusCode);
    }

    [Theory]
    [InlineData(PreventiveMaintenanceFormStatusCatalog.Submitted)]
    [InlineData(PreventiveMaintenanceFormStatusCatalog.Acknowledged)]
    public async Task Submitted_or_acknowledged_forms_are_immutable(string status)
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, $"FE-FORM-{status}", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "Original draft row");
        await application.SetFormStatusAsync(form.Id, status);

        var update = await client.PutAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{row.Id}",
            UpdateDraftInspectionRequest("Updated draft row"));
        var delete = await client.DeleteAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{row.Id}");

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Draft_rows_are_excluded_from_official_history_and_search_projection()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-003", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "mahina ang pressure");

        var history = await client.GetAsync($"/api/v1/inspections/history/{asset.Id}");
        var list = await client.GetAsync("/api/v1/inspections");
        var detail = await client.GetAsync($"/api/v1/inspections/{row.Id}");
        await using var scope = application.Services.CreateAsyncScope();
        var projector = scope.ServiceProvider.GetRequiredService<MaintenanceSearchDocumentProjector>();
        var rebuild = await projector.RebuildAsync();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        Assert.Empty(await history.Content.ReadFromJsonAsync<List<InspectionHistoryResponse>>() ?? []);
        Assert.Empty(await list.Content.ReadFromJsonAsync<List<InspectionResponse>>() ?? []);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(0, rebuild.Total);
        Assert.Empty(await context.MaintenanceSearchDocuments.ToListAsync());
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Gsd)]
    [InlineData(AuthRoleCatalog.Inspector)]
    public async Task Form_routes_require_authentication_and_allow_gsd_or_inspector_roles(string role)
    {
        await using var unauthenticatedApplication = new UnauthenticatedTestApplicationFactory();
        using var unauthenticatedClient = unauthenticatedApplication.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await unauthenticatedClient.GetAsync("/api/v1/preventive-maintenance-forms")).StatusCode);

        await using var application = new TestApplicationFactory(role);
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var schedule = await application.SeedScheduleAsync("fire-extinguisher");
        var form = await CreateFormAsync(client, "fire-extinguisher");

        var list = await client.GetAsync("/api/v1/preventive-maintenance-forms");
        var detail = await client.GetAsync($"/api/v1/preventive-maintenance-forms/{form.Id}");
        var ownRow = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(schedule.Id, "Own inspector row"));

        list.EnsureSuccessStatusCode();
        detail.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, ownRow.StatusCode);

        if (role == AuthRoleCatalog.Inspector)
        {
            var otherSchedule = await application.SeedScheduleAsync("fire-extinguisher");
            var mismatchedInspector = await client.PostAsJsonAsync(
                $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
                new
                {
                    scheduleId = otherSchedule.Id,
                    inspectorUserId = Guid.NewGuid(),
                    dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
                    isOperational = false,
                    remarks = "Mismatched inspector"
                });

            Assert.Equal(HttpStatusCode.Forbidden, mismatchedInspector.StatusCode);

            var anotherSchedule = await application.SeedScheduleAsync("fire-extinguisher");
            var otherInspectorRow = await application.AddDraftRowAsync(
                form.Id,
                anotherSchedule.Id,
                Guid.NewGuid());
            var deleteOtherInspectorRow = await client.DeleteAsync(
                $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{otherInspectorRow.Id}");
            var updateOtherInspectorRow = await client.PutAsJsonAsync(
                $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{otherInspectorRow.Id}",
                UpdateDraftInspectionRequest("Attempted ownership bypass"));

            Assert.Equal(HttpStatusCode.Forbidden, deleteOtherInspectorRow.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, updateOtherInspectorRow.StatusCode);
        }
    }

    [Fact]
    public async Task Submitted_rows_are_hidden_while_acknowledged_rows_are_official_and_projected()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-004", "fire-extinguisher");
        var submittedSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var acknowledgedSchedule = await CreateScheduleAsync(client, asset.Id, 2);
        var submittedForm = await CreateFormAsync(client, asset.AssetCategory);
        var acknowledgedForm = await CreateFormAsync(client, asset.AssetCategory);
        var submittedRow = await AddInspectionRowAsync(client, submittedForm.Id, submittedSchedule.Id, "Submitted only");
        var acknowledgedRow = await AddInspectionRowAsync(client, acknowledgedForm.Id, acknowledgedSchedule.Id, "Acknowledged official row");
        await application.SetFormStatusAsync(submittedForm.Id, PreventiveMaintenanceFormStatusCatalog.Submitted);
        await application.SetFormStatusAsync(acknowledgedForm.Id, PreventiveMaintenanceFormStatusCatalog.Acknowledged);

        var history = await client.GetAsync($"/api/v1/inspections/history/{asset.Id}");
        var list = await client.GetAsync("/api/v1/inspections");
        var submittedDetail = await client.GetAsync($"/api/v1/inspections/{submittedRow.Id}");
        var acknowledgedDetail = await client.GetAsync($"/api/v1/inspections/{acknowledgedRow.Id}");
        await using var scope = application.Services.CreateAsyncScope();
        var projector = scope.ServiceProvider.GetRequiredService<MaintenanceSearchDocumentProjector>();
        var rebuild = await projector.RebuildAsync();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var historyRows = await history.Content.ReadFromJsonAsync<List<InspectionHistoryResponse>>();
        var inspectionRows = await list.Content.ReadFromJsonAsync<List<InspectionResponse>>();
        Assert.Equal([acknowledgedRow.Id], historyRows!.Select(row => row.Id).ToArray());
        Assert.Equal([acknowledgedRow.Id], inspectionRows!.Select(row => row.Id).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, submittedDetail.StatusCode);
        acknowledgedDetail.EnsureSuccessStatusCode();
        Assert.Equal(1, rebuild.Total);
        Assert.Equal([acknowledgedRow.Id], (await context.MaintenanceSearchDocuments
            .Select(document => document.InspectionId)
            .ToListAsync()).ToArray());
    }

    [Fact]
    public async Task Submitting_a_draft_form_assigns_provisional_file_number_and_metadata()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-SUBMIT-001", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, schedule.Id, "Draft submission row");

        var response = await client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/submit",
            content: null);

        response.EnsureSuccessStatusCode();
        var submitted = await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>();
        Assert.NotNull(submitted);
        Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Submitted, submitted.Status);
        Assert.Matches("^PMF-[0-9]{4}-[0-9]{4}$", submitted.FileNumber);
        Assert.Equal(TestAuthenticationHandler.UserId, submitted.SubmittedByUserId);
        Assert.NotNull(submitted.SubmittedAt);

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var persistedSchedule = await context.PreventiveMaintenanceSchedules.SingleAsync(candidate => candidate.Id == schedule.Id);
        Assert.Equal(ScheduleStatusCatalog.Due, persistedSchedule.Status);
        Assert.Null(persistedSchedule.CompletedAt);
        Assert.Empty(await context.MaintenanceSearchDocuments.ToListAsync());
    }

    [Fact]
    public async Task Submission_rejects_empty_repeated_or_unauthorized_forms()
    {
        await using var gsdApplication = new TestApplicationFactory();
        using var gsdClient = gsdApplication.CreateClient();
        await gsdApplication.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(gsdClient, "FE-FORM-SUBMIT-002", "fire-extinguisher");
        var emptyForm = await CreateFormAsync(gsdClient, asset.AssetCategory);
        var emptySubmission = await gsdClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{emptyForm.Id}/submit",
            content: null);

        var schedule = await CreateScheduleAsync(gsdClient, asset.Id, 1);
        var completedForm = await CreateFormAsync(gsdClient, asset.AssetCategory);
        await AddInspectionRowAsync(gsdClient, completedForm.Id, schedule.Id, "Ready to submit");
        var firstSubmission = await gsdClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{completedForm.Id}/submit",
            content: null);
        var repeatedSubmission = await gsdClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{completedForm.Id}/submit",
            content: null);

        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var inspectorClient = inspectorApplication.CreateClient();
        await inspectorApplication.EnsureAuthenticatedUserAsync();
        var inspectorSchedule = await inspectorApplication.SeedScheduleAsync("fire-extinguisher");
        var otherCreatorForm = await inspectorApplication.SeedDraftFormAsync(
            inspectorSchedule.Id,
            Guid.NewGuid(),
            TestAuthenticationHandler.UserId);
        var unauthorizedSubmission = await inspectorClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{otherCreatorForm.Id}/submit",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, emptySubmission.StatusCode);
        firstSubmission.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, repeatedSubmission.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedSubmission.StatusCode);
    }

    [Fact]
    public async Task Acknowledgement_completes_schedules_and_publishes_history_and_projection()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-ACK-001", "fire-extinguisher");
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 2);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var firstRow = await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, "First acknowledged row");
        var secondRow = await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second acknowledged row");
        (await client.PostAsync($"/api/v1/preventive-maintenance-forms/{form.Id}/submit", content: null))
            .EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/acknowledge",
            AcknowledgementRequest());

        response.EnsureSuccessStatusCode();
        var acknowledgement = await response.Content
            .ReadFromJsonAsync<PreventiveMaintenanceAcknowledgementResponse>();
        Assert.NotNull(acknowledgement);
        Assert.Equal(form.Id, acknowledgement.FormId);
        Assert.Equal("Department Head", acknowledgement.SignatoryPosition);
        Assert.Equal("image/png", acknowledgement.SignatureContentType);
        Assert.Equal(TestAuthenticationHandler.UserId, acknowledgement.CapturedByUserId);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(TestPngSignatureBase64))),
            acknowledgement.SignatureChecksum);

        var history = await client.GetFromJsonAsync<List<InspectionHistoryResponse>>(
            $"/api/v1/inspections/history/{asset.Id}");
        Assert.Equivalent(
            new[] { firstRow.Id, secondRow.Id },
            history!.Select(row => row.Id));

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var persistedForm = await context.PreventiveMaintenanceForms
            .Include(candidate => candidate.Acknowledgement)
            .SingleAsync(candidate => candidate.Id == form.Id);
        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => schedule.Id == firstSchedule.Id || schedule.Id == secondSchedule.Id)
            .ToListAsync();
        var projectedInspectionIds = await context.MaintenanceSearchDocuments
            .Where(document => document.InspectionId == firstRow.Id || document.InspectionId == secondRow.Id)
            .Select(document => document.InspectionId)
            .ToListAsync();

        Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Acknowledged, persistedForm.Status);
        Assert.NotNull(persistedForm.Acknowledgement);
        Assert.All(schedules, schedule =>
        {
            Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
            Assert.Equal(acknowledgement.AcknowledgedAt, schedule.CompletedAt);
        });
        Assert.Equivalent(new[] { firstRow.Id, secondRow.Id }, projectedInspectionIds);
        Assert.Empty(await context.MaintenanceSearchDocumentEmbeddings.ToListAsync());
    }

    [Fact]
    public async Task Acknowledgement_rejects_invalid_repeated_wrong_status_or_unauthorized_requests()
    {
        await using var gsdApplication = new TestApplicationFactory();
        using var gsdClient = gsdApplication.CreateClient();
        await gsdApplication.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(gsdClient, "FE-FORM-ACK-002", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(gsdClient, asset.Id, 1);
        var submittedForm = await CreateFormAsync(gsdClient, asset.AssetCategory);
        await AddInspectionRowAsync(gsdClient, submittedForm.Id, schedule.Id, "Acknowledgement rejection row");
        (await gsdClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{submittedForm.Id}/submit",
            content: null)).EnsureSuccessStatusCode();

        var invalid = await gsdClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{submittedForm.Id}/acknowledge",
            AcknowledgementRequest("not-base64"));
        var accepted = await gsdClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{submittedForm.Id}/acknowledge",
            AcknowledgementRequest());
        var repeated = await gsdClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{submittedForm.Id}/acknowledge",
            AcknowledgementRequest());

        var draftForm = await CreateFormAsync(gsdClient, asset.AssetCategory);
        var wrongStatus = await gsdClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{draftForm.Id}/acknowledge",
            AcknowledgementRequest());

        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var inspectorClient = inspectorApplication.CreateClient();
        await inspectorApplication.EnsureAuthenticatedUserAsync();
        var inspectorSchedule = await inspectorApplication.SeedScheduleAsync("fire-extinguisher");
        var otherCreatorForm = await inspectorApplication.SeedDraftFormAsync(
            inspectorSchedule.Id,
            Guid.NewGuid(),
            TestAuthenticationHandler.UserId);
        await inspectorApplication.SetFormStatusAsync(
            otherCreatorForm.Id,
            PreventiveMaintenanceFormStatusCatalog.Submitted);
        var unauthorized = await inspectorClient.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{otherCreatorForm.Id}/acknowledge",
            AcknowledgementRequest());

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        accepted.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrongStatus.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Acknowledged_form_returns_corrective_handoff_rows_with_recommended_actions_only()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-HANDOFF-001", "fire-extinguisher");
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 3);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 4);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var actionableRow = await AddInspectionRowAsync(
            client,
            form.Id,
            firstSchedule.Id,
            "Low pressure finding",
            "Replace the pressure gauge.");
        await AddInspectionRowAsync(
            client,
            form.Id,
            secondSchedule.Id,
            "Operational finding",
            null);
        (await client.PostAsync($"/api/v1/preventive-maintenance-forms/{form.Id}/submit", content: null))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/acknowledge",
            AcknowledgementRequest())).EnsureSuccessStatusCode();

        var response = await client.GetAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/corrective-handoff");

        response.EnsureSuccessStatusCode();
        var handoff = await response.Content.ReadFromJsonAsync<CorrectiveMaintenanceHandoffResponse>();
        Assert.NotNull(handoff);
        Assert.Equal(form.Id, handoff.FormId);
        Assert.NotNull(handoff.FileNumber);
        Assert.Equal("GSD", handoff.Department);
        Assert.Equal("Main Building", handoff.Building);
        Assert.Equal(asset.AssetCategory, handoff.AssetCategory);
        Assert.True(handoff.HasCorrectiveActionRows);
        var row = Assert.Single(handoff.Rows);
        Assert.Equal(actionableRow.Id, row.InspectionId);
        Assert.Equal(asset.AssetCode, row.AssetDeviceNumber);
        Assert.Equal(asset.AssetCode, row.AssetCode);
        Assert.Equal("Test Area", row.Location);
        Assert.Equal("Low pressure finding", row.FindingOrRemarks);
        Assert.False(row.IsOperational);
        Assert.Equal("Replace the pressure gauge.", row.RecommendedCorrectiveAction);
        Assert.Equal(TestAuthenticationHandler.UserId, row.SkilledWorkerUserId);
        Assert.Equal("Form Drafts User", row.SkilledWorkerIdentity);
        var responseJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("signatureData", responseJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("signatureChecksum", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Corrective_handoff_rejects_draft_submitted_unauthorized_and_missing_forms()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var draft = await CreateFormAsync(client, "fire-extinguisher");
        var submitted = await CreateFormAsync(client, "fire-extinguisher");
        await application.SetFormStatusAsync(submitted.Id, PreventiveMaintenanceFormStatusCatalog.Submitted);

        var draftResponse = await client.GetAsync(
            $"/api/v1/preventive-maintenance-forms/{draft.Id}/corrective-handoff");
        var submittedResponse = await client.GetAsync(
            $"/api/v1/preventive-maintenance-forms/{submitted.Id}/corrective-handoff");
        var missingResponse = await client.GetAsync(
            $"/api/v1/preventive-maintenance-forms/{Guid.NewGuid()}/corrective-handoff");

        await using var inspectorApplication = new TestApplicationFactory(AuthRoleCatalog.Inspector);
        using var inspectorClient = inspectorApplication.CreateClient();
        await inspectorApplication.EnsureAuthenticatedUserAsync();
        var unauthorizedResponse = await inspectorClient.GetAsync(
            $"/api/v1/preventive-maintenance-forms/{Guid.NewGuid()}/corrective-handoff");

        Assert.Equal(HttpStatusCode.Conflict, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, submittedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedResponse.StatusCode);
    }

    private static async Task<AssetResponse> CreateAssetAsync(HttpClient client, string assetCode, string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory,
            building = "Main Building",
            department = "GSD",
            location = "Test Area"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetResponse>())!;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(HttpClient client, Guid assetId, int month)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = new DateTimeOffset(2026, month, 10, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduleResponse>())!;
    }

    private static async Task<PreventiveMaintenanceFormResponse> CreateFormAsync(HttpClient client, string assetCategory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/preventive-maintenance-forms/", new
        {
            assetCategory,
            building = "Main Building",
            department = "GSD",
            periodType = "Quarter",
            quarter = "Q1",
            year = 2026
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>())!;
    }

    private static async Task<DraftInspectionRowResponse> AddInspectionRowAsync(
        HttpClient client,
        Guid formId,
        Guid scheduleId,
        string remarks,
        string? actionsRecommendations = "Inspect during final submission.")
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{formId}/inspections",
            DraftInspectionRequest(scheduleId, remarks, actionsRecommendations));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DraftInspectionRowResponse>())!;
    }

    private static object DraftInspectionRequest(
        Guid scheduleId,
        string remarks,
        string? actionsRecommendations = "Inspect during final submission.")
    {
        return new
        {
            scheduleId,
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            isOperational = false,
            remarks,
            actionsRecommendations
        };
    }

    private static object UpdateDraftInspectionRequest(string remarks)
    {
        return new
        {
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            isOperational = false,
            remarks,
            actionsRecommendations = "Inspect during final submission."
        };
    }

    private static object AcknowledgementRequest(string signatureData = TestPngSignatureBase64)
    {
        return new
        {
            signatoryName = "Fictional Department Head",
            signatoryPosition = "Department Head",
            signatureData,
            signatureContentType = "image/png"
        };
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-form-drafts-{Guid.NewGuid()}";
        private readonly string[] roles;

        public TestApplicationFactory(params string[] roles)
        {
            this.roles = roles.Length == 0 ? [AuthRoleCatalog.Gsd] : roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task EnsureAuthenticatedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            if (await context.Users.AnyAsync(user => user.Id == TestAuthenticationHandler.UserId))
            {
                return;
            }

            context.Users.Add(new ApplicationUser
            {
                Id = TestAuthenticationHandler.UserId,
                UserName = "form-drafts@unipm.local",
                NormalizedUserName = "FORM-DRAFTS@UNIPM.LOCAL",
                Email = "form-drafts@unipm.local",
                NormalizedEmail = "FORM-DRAFTS@UNIPM.LOCAL",
                EmailConfirmed = true,
                DisplayName = "Form Drafts User",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        public async Task SetFormStatusAsync(Guid formId, string status)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var form = await context.PreventiveMaintenanceForms.SingleAsync(candidate => candidate.Id == formId);
            form.Status = status;
            await context.SaveChangesAsync();
        }

        public async Task<ScheduleResponse> SeedScheduleAsync(string assetCategory)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"FORM-ROLE-{Guid.NewGuid():N}"[..24],
                AssetCategory = assetCategory,
                Building = "Main Building",
                Department = "GSD",
                Location = "Test Area",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.FromHours(8)),
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = 2026,
                Status = "Due",
                AssignedToUserId = TestAuthenticationHandler.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();
            return new ScheduleResponse(schedule.Id);
        }

        public async Task<DraftInspectionRowResponse> AddDraftRowAsync(
            Guid formId,
            Guid scheduleId,
            Guid inspectorUserId)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules
                .SingleAsync(candidate => candidate.Id == scheduleId);
            var now = DateTimeOffset.UtcNow;
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                PreventiveMaintenanceFormId = formId,
                AssetId = schedule.AssetId,
                InspectorUserId = inspectorUserId,
                DateInspected = now,
                IsOperational = false,
                Remarks = "Other inspector draft row",
                CreatedAt = now,
                UpdatedAt = now
            };
            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();
            return DraftInspectionRowResponse.FromInspection(inspection);
        }

        public async Task<PreventiveMaintenanceFormResponse> SeedDraftFormAsync(
            Guid scheduleId,
            Guid createdByUserId,
            Guid inspectorUserId)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules
                .Include(candidate => candidate.Asset)
                .SingleAsync(candidate => candidate.Id == scheduleId);
            var now = DateTimeOffset.UtcNow;
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = schedule.Asset!.AssetCategory,
                Building = "Main Building",
                Department = "GSD",
                PeriodType = "Quarter",
                Quarter = "Q1",
                Year = 2026,
                Status = PreventiveMaintenanceFormStatusCatalog.Draft,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                PreventiveMaintenanceFormId = form.Id,
                AssetId = schedule.AssetId,
                InspectorUserId = inspectorUserId,
                DateInspected = now,
                IsOperational = false,
                Remarks = "Inspector-owned draft row",
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PreventiveMaintenanceForms.Add(form);
            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();
            return PreventiveMaintenanceFormResponse.FromForm(form);
        }
    }

    private sealed class UnauthenticatedTestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-form-drafts-unauthenticated-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        }
    }

    private sealed record AssetResponse(Guid Id, string AssetCategory);

    private sealed record ScheduleResponse(Guid Id);
}
