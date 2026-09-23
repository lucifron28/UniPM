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
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 1, day: 11);

        var form = await CreateFormAsync(client, asset.AssetCategory);
        var firstRow = await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, "First draft row");
        await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second draft row");

        var response = await client.GetAsync($"/api/v1/preventive-maintenance-forms/{form.Id}");

        response.EnsureSuccessStatusCode();
        var persisted = await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormResponse>();
        Assert.NotNull(persisted);
        Assert.Equal("Draft", persisted.Status);
        Assert.Null(persisted.FieldWorkCompletedAt);
        Assert.Equal(2, persisted.Inspections.Count);
        Assert.Equal(asset.AssetCode, firstRow.AssetCode);
        Assert.Equal(asset.Location, firstRow.Location);
        Assert.Equal("Form Drafts User", firstRow.SkilledWorkerIdentity);
        Assert.All(persisted.Inspections, row =>
        {
            Assert.Equal(asset.AssetCode, row.AssetCode);
            Assert.Equal(asset.Location, row.Location);
            Assert.Equal("Form Drafts User", row.SkilledWorkerIdentity);
        });
        Assert.Equivalent(
            new[] { firstSchedule.Id, secondSchedule.Id },
            persisted.Inspections.Select(row => row.ScheduleId));
    }

    [Theory]
    [InlineData("fire-extinguisher")]
    [InlineData("fire-alarm")]
    [InlineData("emergency-light")]
    [InlineData("water-drinking-station")]
    public async Task Confirmed_category_forms_preserve_visible_inspection_fields(string assetCategory)
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, $"PM-{assetCategory}", assetCategory);
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, assetCategory);

        var rowResponse = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(
                schedule.Id,
                "Visible form remarks",
                dateAccomplished: new DateTimeOffset(
                    2026,
                    1,
                    16,
                    8,
                    0,
                    0,
                    TimeSpan.FromHours(8)),
                waterReplaceCarbonFilter: assetCategory == "water-drinking-station",
                waterReplaceSedimentFilter: false,
                waterCheckUvLight: true));

        rowResponse.EnsureSuccessStatusCode();
        var row = await rowResponse.Content.ReadFromJsonAsync<DraftInspectionRowResponse>();
        var expectedCarbonFilter = assetCategory == "water-drinking-station"
            ? true
            : (bool?)null;
        var expectedSedimentFilter = assetCategory == "water-drinking-station"
            ? false
            : (bool?)null;

        Assert.NotNull(row);
        Assert.Equal(
            assetCategory == "water-drinking-station"
                ? new DateTimeOffset(2026, 1, 16, 8, 0, 0, TimeSpan.FromHours(8))
                : null,
            row.DateAccomplished);
        Assert.Equal(expectedCarbonFilter, row.WaterReplaceCarbonFilter);
        Assert.Equal(expectedSedimentFilter, row.WaterReplaceSedimentFilter);
        Assert.Equal(assetCategory == "water-drinking-station" ? true : null, row.WaterCheckUvLight);
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
        var row = await AddInspectionRowAsync(client, form.Id, fireExtinguisherSchedule.Id, "Draft pressure check");

        var duplicate = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireExtinguisherSchedule.Id, "Duplicate"));
        var categoryMismatch = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(fireAlarmSchedule.Id, "Wrong category"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categoryMismatch.StatusCode);

        Assert.Null(row.StartedAt);
        Assert.NotNull(row.CompletedAt);
        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var completedSchedule = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == fireExtinguisherSchedule.Id);
        Assert.Equal(ScheduleStatusCatalog.Completed, completedSchedule.Status);
        Assert.Equal(row.CompletedAt, completedSchedule.CompletedAt);

        var delete = await client.DeleteAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{row.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var restoredSchedule = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == fireExtinguisherSchedule.Id);
        Assert.Equal(ScheduleStatusCatalog.Due, restoredSchedule.Status);
        Assert.Null(restoredSchedule.CompletedAt);
    }

    [Fact]
    public async Task Different_buildings_can_share_a_form_batch()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(
            client,
            "FE-FORM-BUILDING-001",
            "fire-extinguisher",
            building: "South Annex",
            department: "gsd");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);

        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "Different building row");

        Assert.Equal(schedule.Id, row.ScheduleId);
    }

    [Fact]
    public async Task A_pm_cycle_cannot_be_split_across_building_specific_forms()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var mainBuildingAsset = await CreateAssetAsync(
            client,
            "FE-FORM-CYCLE-MAIN-001",
            "fire-extinguisher",
            building: "Main Building",
            department: "GSD");
        var annexAsset = await CreateAssetAsync(
            client,
            "FE-FORM-CYCLE-ANNEX-001",
            "fire-extinguisher",
            building: "South Annex",
            department: "GSD");
        var mainBuildingSchedule = await CreateScheduleAsync(client, mainBuildingAsset.Id, 1, day: 10);
        var annexSchedule = await CreateScheduleAsync(client, annexAsset.Id, 1, day: 11);
        var mainBuildingForm = await CreateFormAsync(
            client,
            mainBuildingAsset.AssetCategory,
            building: "Main Building");
        var annexForm = await CreateFormAsync(
            client,
            annexAsset.AssetCategory,
            building: "South Annex");

        Assert.Equal("Main Building", mainBuildingForm.Building);
        Assert.Equal("South Annex", annexForm.Building);
        var mainBuildingRow = await AddInspectionRowAsync(
            client,
            mainBuildingForm.Id,
            mainBuildingSchedule.Id,
            "Main Building row");
        Assert.NotNull(mainBuildingRow.CompletedAt);

        var secondFormAdd = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{annexForm.Id}/inspections",
            DraftInspectionRequest(annexSchedule.Id, "Attempted split row"));
        Assert.Equal(HttpStatusCode.Conflict, secondFormAdd.StatusCode);

        var legacyCompletedAt = new DateTimeOffset(2026, 1, 16, 8, 0, 0, TimeSpan.FromHours(8));
        await using (var scope = application.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var persistedAnnexSchedule = await context.PreventiveMaintenanceSchedules
                .SingleAsync(candidate => candidate.Id == annexSchedule.Id);
            persistedAnnexSchedule.Status = ScheduleStatusCatalog.Completed;
            persistedAnnexSchedule.CompletedAt = legacyCompletedAt;
            context.InspectionRecords.Add(new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = annexSchedule.Id,
                PreventiveMaintenanceFormId = annexForm.Id,
                AssetId = annexAsset.Id,
                InspectorUserId = TestAuthenticationHandler.UserId,
                DateInspected = legacyCompletedAt,
                CompletedAt = legacyCompletedAt,
                IsOperational = false,
                Remarks = "Legacy split row",
                CreatedAt = legacyCompletedAt,
                UpdatedAt = legacyCompletedAt
            });
            await context.SaveChangesAsync();

            var persistedSchedules = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(candidate => candidate.Id == mainBuildingSchedule.Id || candidate.Id == annexSchedule.Id)
                .ToDictionaryAsync(candidate => candidate.Id);
            var persistedAnnexRow = await context.InspectionRecords
                .AsNoTracking()
                .SingleAsync(candidate => candidate.ScheduleId == annexSchedule.Id);
            Assert.Equal(
                mainBuildingRow.CompletedAt,
                persistedSchedules[mainBuildingSchedule.Id].CompletedAt);
            Assert.Equal(legacyCompletedAt, persistedAnnexRow.CompletedAt);
            Assert.Equal(
                persistedAnnexRow.CompletedAt,
                persistedSchedules[annexSchedule.Id].CompletedAt);
        }

        var mainBuildingSubmit = await client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{mainBuildingForm.Id}/submit",
            content: null);
        var annexSubmit = await client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{annexForm.Id}/submit",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, mainBuildingSubmit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, annexSubmit.StatusCode);

        var mainBuildingAcknowledgement = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{mainBuildingForm.Id}/acknowledge",
            AcknowledgementRequest());
        var annexAcknowledgement = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{annexForm.Id}/acknowledge",
            AcknowledgementRequest());
        Assert.Equal(HttpStatusCode.Conflict, mainBuildingAcknowledgement.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, annexAcknowledgement.StatusCode);
    }

    [Fact]
    public async Task Draft_rows_reject_a_schedule_from_a_different_department()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(
            client,
            "FE-FORM-DEPARTMENT-001",
            "fire-extinguisher",
            department: "Engineering");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(schedule.Id, "Different department row"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Departmentless_assets_cannot_claim_a_pm_form_batch()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(
            client,
            "FE-FORM-NO-DEPARTMENT-001",
            "fire-extinguisher",
            department: null);
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(schedule.Id, "Department-less asset row"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var persistedSchedule = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == schedule.Id);
        var persistedForm = await context.PreventiveMaintenanceForms
            .AsNoTracking()
            .Include(candidate => candidate.Inspections)
            .SingleAsync(candidate => candidate.Id == form.Id);

        Assert.Equal(ScheduleStatusCatalog.Due, persistedSchedule.Status);
        Assert.Null(persistedSchedule.CompletedAt);
        Assert.Null(persistedForm.PmCycle);
        Assert.Empty(persistedForm.Inspections);
    }

    [Fact]
    public async Task Draft_rows_reject_a_schedule_from_a_different_pm_cycle()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-PERIOD-001", "fire-extinguisher");
        var januarySchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var februarySchedule = await CreateScheduleAsync(client, asset.Id, 2);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, januarySchedule.Id, "January cycle row");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(februarySchedule.Id, "Different cycle row"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(ScheduleStatusCatalog.Completed)]
    [InlineData(ScheduleStatusCatalog.Cancelled)]
    public async Task Draft_rows_reject_completed_or_cancelled_schedules(string status)
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, $"FE-FORM-STATUS-{status}", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        await application.SetScheduleStatusAsync(
            schedule.Id,
            status,
            status == ScheduleStatusCatalog.Completed ? DateTimeOffset.UtcNow : null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(schedule.Id, "Ineligible schedule row"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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

    [Theory]
    [InlineData(AuthRoleCatalog.Inspector, "own", HttpStatusCode.Created)]
    [InlineData(AuthRoleCatalog.Inspector, "unassigned", HttpStatusCode.Created)]
    [InlineData(AuthRoleCatalog.Inspector, "other", HttpStatusCode.Forbidden)]
    [InlineData(AuthRoleCatalog.Gsd, "other", HttpStatusCode.Created)]
    public async Task Adding_an_inspection_enforces_schedule_assignment(
        string role,
        string assignment,
        HttpStatusCode expectedStatus)
    {
        await using var application = new TestApplicationFactory(role);
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var schedule = await application.SeedScheduleAsync("fire-extinguisher");
        Guid? assignedToUserId = assignment switch
        {
            "own" => TestAuthenticationHandler.UserId,
            "unassigned" => null,
            _ => Guid.NewGuid()
        };
        await application.SetScheduleAssigneeAsync(schedule.Id, assignedToUserId);
        var form = await CreateFormAsync(client, "fire-extinguisher");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections",
            DraftInspectionRequest(schedule.Id, "Assignment authorization check"));

        Assert.Equal(expectedStatus, response.StatusCode);
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
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 1, day: 11);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var firstRow = await AddInspectionRowAsync(client, form.Id, schedule.Id, "Draft submission row");
        var secondRow = await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second draft submission row");

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
        Assert.Equal(new[] { firstRow.CompletedAt, secondRow.CompletedAt }.Max(), submitted.FieldWorkCompletedAt);

        await using var scope = application.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var persistedSchedules = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Where(candidate => candidate.Id == schedule.Id || candidate.Id == secondSchedule.Id)
            .ToDictionaryAsync(candidate => candidate.Id);
        Assert.Equal(ScheduleStatusCatalog.Completed, persistedSchedules[schedule.Id].Status);
        Assert.Equal(firstRow.CompletedAt, persistedSchedules[schedule.Id].CompletedAt);
        Assert.Equal(ScheduleStatusCatalog.Completed, persistedSchedules[secondSchedule.Id].Status);
        Assert.Equal(secondRow.CompletedAt, persistedSchedules[secondSchedule.Id].CompletedAt);
        Assert.Empty(await context.MaintenanceSearchDocuments.ToListAsync());
    }

    [Fact]
    public async Task Submission_is_blocked_when_an_eligible_schedule_in_the_batch_has_no_row()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-SUBMIT-BATCH-001", "fire-extinguisher");
        var includedSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        _ = await CreateScheduleAsync(client, asset.Id, 1, day: 11);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        await AddInspectionRowAsync(client, form.Id, includedSchedule.Id, "Only one of two batch schedules");

        var response = await client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/submit",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_schedule_completion_does_not_complete_its_inspection_row()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-LEGACY-COMPLETION-001", "fire-extinguisher");
        var schedule = await CreateScheduleAsync(client, asset.Id, 1);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var row = await AddInspectionRowAsync(client, form.Id, schedule.Id, "Legacy completion row");
        var legacyScheduleCompletedAt = new DateTimeOffset(2024, 1, 15, 8, 0, 0, TimeSpan.Zero);

        await using (var scope = application.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var inspection = await context.InspectionRecords.SingleAsync(candidate => candidate.Id == row.Id);
            var persistedSchedule = await context.PreventiveMaintenanceSchedules.SingleAsync(candidate => candidate.Id == schedule.Id);
            inspection.CompletedAt = null;
            persistedSchedule.Status = ScheduleStatusCatalog.Completed;
            persistedSchedule.CompletedAt = legacyScheduleCompletedAt;
            await context.SaveChangesAsync();
        }

        var response = await client.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{form.Id}/submit",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verificationScope = application.Services.CreateAsyncScope();
        var verificationFactory = verificationScope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var verificationContext = await verificationFactory.CreateDbContextAsync();
        var persistedInspection = await verificationContext.InspectionRecords
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == row.Id);
        var persistedScheduleAfterSubmit = await verificationContext.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == schedule.Id);
        Assert.Null(persistedInspection.CompletedAt);
        Assert.Equal(ScheduleStatusCatalog.Completed, persistedScheduleAfterSubmit.Status);
        Assert.Equal(legacyScheduleCompletedAt, persistedScheduleAfterSubmit.CompletedAt);
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

        await using var incompleteApplication = new TestApplicationFactory();
        using var incompleteClient = incompleteApplication.CreateClient();
        await incompleteApplication.EnsureAuthenticatedUserAsync();
        var incompleteSchedule = await incompleteApplication.SeedScheduleAsync("fire-extinguisher");
        var incompleteForm = await incompleteApplication.SeedDraftFormAsync(
            incompleteSchedule.Id,
            TestAuthenticationHandler.UserId,
            TestAuthenticationHandler.UserId);
        var incompleteSubmission = await incompleteClient.PostAsync(
            $"/api/v1/preventive-maintenance-forms/{incompleteForm.Id}/submit",
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
        Assert.Equal(HttpStatusCode.Conflict, incompleteSubmission.StatusCode);
        firstSubmission.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, repeatedSubmission.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedSubmission.StatusCode);
    }

    [Fact]
    public async Task Acknowledgement_preserves_schedule_completion_and_publishes_history_and_projection()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        await application.EnsureAuthenticatedUserAsync();
        var asset = await CreateAssetAsync(client, "FE-FORM-ACK-001", "fire-extinguisher");
        var firstSchedule = await CreateScheduleAsync(client, asset.Id, 1);
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 1, day: 11);
        var form = await CreateFormAsync(client, asset.AssetCategory);
        var firstRow = await AddInspectionRowAsync(client, form.Id, firstSchedule.Id, "First acknowledged row");
        var secondRow = await AddInspectionRowAsync(client, form.Id, secondSchedule.Id, "Second acknowledged row");
        (await client.PostAsync($"/api/v1/preventive-maintenance-forms/{form.Id}/submit", content: null))
            .EnsureSuccessStatusCode();

        List<PreventiveMaintenanceSchedule> schedulesBeforeAcknowledgement;
        await using (var beforeAcknowledgementScope = application.Services.CreateAsyncScope())
        {
            var beforeAcknowledgementFactory = beforeAcknowledgementScope.ServiceProvider
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var beforeAcknowledgementContext = await beforeAcknowledgementFactory.CreateDbContextAsync();
            schedulesBeforeAcknowledgement = await beforeAcknowledgementContext.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => schedule.Id == firstSchedule.Id || schedule.Id == secondSchedule.Id)
                .ToListAsync();
        }
        Assert.Equal(2, schedulesBeforeAcknowledgement.Count);
        Assert.All(schedulesBeforeAcknowledgement, schedule =>
        {
            Assert.Equal(ScheduleStatusCatalog.Completed, schedule.Status);
            Assert.NotNull(schedule.CompletedAt);
        });
        Assert.Equal(firstRow.CompletedAt, schedulesBeforeAcknowledgement.Single(schedule => schedule.Id == firstSchedule.Id).CompletedAt);
        Assert.Equal(secondRow.CompletedAt, schedulesBeforeAcknowledgement.Single(schedule => schedule.Id == secondSchedule.Id).CompletedAt);

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
        var schedulesAfterAcknowledgement = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Where(schedule => schedule.Id == firstSchedule.Id || schedule.Id == secondSchedule.Id)
            .ToListAsync();
        var projectedInspectionIds = await context.MaintenanceSearchDocuments
            .Where(document => document.InspectionId == firstRow.Id || document.InspectionId == secondRow.Id)
            .Select(document => document.InspectionId)
            .ToListAsync();

        Assert.Equal(PreventiveMaintenanceFormStatusCatalog.Acknowledged, persistedForm.Status);
        Assert.NotNull(persistedForm.Acknowledgement);
        Assert.All(schedulesAfterAcknowledgement, schedule =>
        {
            var previousSchedule = schedulesBeforeAcknowledgement.Single(previous => previous.Id == schedule.Id);
            Assert.Equal(previousSchedule.Status, schedule.Status);
            Assert.Equal(previousSchedule.CompletedAt, schedule.CompletedAt);
            Assert.Equal(previousSchedule.UpdatedAt, schedule.UpdatedAt);
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
        var secondSchedule = await CreateScheduleAsync(client, asset.Id, 3, day: 11);
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
        Assert.Null(row.AssetDeviceNumber);
        Assert.Equal("FE-HANDOFF-001", row.AssetCode);
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

    private static async Task<AssetResponse> CreateAssetAsync(
        HttpClient client,
        string assetCode,
        string assetCategory,
        string building = "Main Building",
        string? department = "GSD")
    {
        var response = await client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode,
            assetCategory,
            building,
            department,
            location = "Test Area"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetResponse>())!;
    }

    private static async Task<ScheduleResponse> CreateScheduleAsync(
        HttpClient client,
        Guid assetId,
        int month,
        string periodType = "Quarter",
        string? quarter = "Q1",
        int? year = 2026,
        int day = 10)
    {
        var response = await client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId,
            scheduleDate = new DateTimeOffset(2026, month, day, 8, 0, 0, TimeSpan.FromHours(8)),
            periodType,
            quarter,
            year
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduleResponse>())!;
    }

    private static async Task<PreventiveMaintenanceFormResponse> CreateFormAsync(
        HttpClient client,
        string assetCategory,
        string building = "Main Building",
        string department = "GSD")
    {
        var response = await client.PostAsJsonAsync("/api/v1/preventive-maintenance-forms/", new
        {
            assetCategory,
            building,
            department,
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
        string? actionsRecommendations = "Inspect during final submission.",
        DateTimeOffset? dateAccomplished = null,
        bool? waterReplaceCarbonFilter = null,
        bool? waterReplaceSedimentFilter = null,
        bool? waterCheckUvLight = null)
    {
        return new
        {
            scheduleId,
            inspectorUserId = TestAuthenticationHandler.UserId,
            dateInspected = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(8)),
            dateAccomplished,
            isOperational = false,
            remarks,
            actionsRecommendations,
            waterReplaceCarbonFilter,
            waterReplaceSedimentFilter,
            waterCheckUvLight
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

        public async Task SetScheduleStatusAsync(
            Guid scheduleId,
            string status,
            DateTimeOffset? completedAt = null)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules.SingleAsync(candidate => candidate.Id == scheduleId);
            schedule.Status = status;
            schedule.CompletedAt = completedAt;
            await context.SaveChangesAsync();
        }

        public async Task SetScheduleAssigneeAsync(Guid scheduleId, Guid? assignedToUserId)
        {
            await using var scope = Services.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules.SingleAsync(candidate => candidate.Id == scheduleId);
            schedule.AssignedToUserId = assignedToUserId;
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

    private sealed record AssetResponse(
        Guid Id,
        string AssetCode,
        string AssetCategory,
        string? Location);

    private sealed record ScheduleResponse(Guid Id);
}
