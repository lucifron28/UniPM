using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class InspectionWmsReferralEndpointsTests
{
    [Fact]
    public async Task Gsd_can_record_correct_and_read_audited_wms_number_without_changing_inspection_or_schedule()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var scenario = await application.CreateScenarioAsync();
        var before = await application.ReadWorkflowStateAsync(scenario);

        var firstResponse = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/wms-referral",
            new { externalPmNumber = "  WMS-PM-2026-0042  ", expectedRevision = 0 });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<InspectionWmsReferralResponse>();
        Assert.NotNull(first);
        Assert.Equal("WMS-PM-2026-0042", first.ExternalPmNumber);
        Assert.Equal(1, first.Revision);
        Assert.Equal(TestAuthenticationHandler.UserId, first.RecordedByUserId);
        Assert.Equal(InspectionFollowUpStatusCatalog.ReferredToWms, first.FollowUpStatus);

        var correctionResponse = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-2026-0043", expectedRevision = 1 });
        Assert.Equal(HttpStatusCode.OK, correctionResponse.StatusCode);

        var detailResponse = await client.GetAsync($"/api/v1/inspections/{scenario.InspectionId}/wms-referral");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<InspectionWmsReferralDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("WMS-PM-2026-0043", detail.ExternalPmNumber);
        Assert.Equal(2, detail.Revision);
        Assert.True(detail.CanRecordReferral);
        Assert.Collection(
            detail.Audit,
            initial =>
            {
                Assert.Null(initial.PreviousExternalPmNumber);
                Assert.Equal("WMS-PM-2026-0042", initial.NewExternalPmNumber);
                Assert.Equal(1, initial.Revision);
                Assert.Equal(TestAuthenticationHandler.UserId, initial.ChangedByUserId);
            },
            correction =>
            {
                Assert.Equal("WMS-PM-2026-0042", correction.PreviousExternalPmNumber);
                Assert.Equal("WMS-PM-2026-0043", correction.NewExternalPmNumber);
                Assert.Equal(2, correction.Revision);
                Assert.Equal(TestAuthenticationHandler.UserId, correction.ChangedByUserId);
            });

        var noOpResponse = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-2026-0043", expectedRevision = 2 });
        Assert.Equal(HttpStatusCode.OK, noOpResponse.StatusCode);

        var after = await application.ReadWorkflowStateAsync(scenario);
        Assert.Equal(before.InspectionUpdatedAt, after.InspectionUpdatedAt);
        Assert.Equal(before.InspectionCompletedAt, after.InspectionCompletedAt);
        Assert.Equal(before.ScheduleUpdatedAt, after.ScheduleUpdatedAt);
        Assert.Equal(before.ScheduleStatus, after.ScheduleStatus);
        Assert.Equal(before.FormUpdatedAt, after.FormUpdatedAt);
        Assert.Equal(before.FormStatus, after.FormStatus);
        Assert.Equal(before.AcknowledgementAt, after.AcknowledgementAt);
        Assert.Equal(before.SignatoryName, after.SignatoryName);
        Assert.Equal(2, after.AuditCount);

        var secondInspection = await application.CreateScenarioAsync();
        var reusedNumber = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{secondInspection.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-2026-0042", expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.OK, reusedNumber.StatusCode);
    }

    [Fact]
    public async Task Referral_requires_acknowledgement_completed_nonoperational_work_and_a_linked_form()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var notAcknowledged = await application.CreateScenarioAsync(formStatus: PreventiveMaintenanceFormStatusCatalog.Draft);
        var missingAcknowledgement = await application.CreateScenarioAsync(includeAcknowledgement: false);
        var operational = await application.CreateScenarioAsync(isOperational: true);
        var incomplete = await application.CreateScenarioAsync(workCompleted: false);
        var legacy = await application.CreateScenarioAsync(linkForm: false);

        foreach (var scenario in new[] { notAcknowledged, missingAcknowledgement, operational, incomplete, legacy })
        {
            var response = await client.PutAsJsonAsync(
                $"/api/v1/inspections/{scenario.InspectionId}/wms-referral",
                new { externalPmNumber = "WMS-PM-1", expectedRevision = 0 });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        var legacyDetail = await client.GetAsync($"/api/v1/inspections/{legacy.InspectionId}/wms-referral");
        legacyDetail.EnsureSuccessStatusCode();
        var detail = await legacyDetail.Content.ReadFromJsonAsync<InspectionWmsReferralDetailResponse>();
        Assert.NotNull(detail);
        Assert.False(detail.CanRecordReferral);
        Assert.Empty(detail.Audit);
    }

    [Theory]
    [InlineData(AuthRoleCatalog.Admin)]
    [InlineData(AuthRoleCatalog.Inspector)]
    [InlineData(AuthRoleCatalog.Supervisor)]
    public async Task Referral_is_forbidden_for_non_gsd_roles(string role)
    {
        await using var application = new TestApplicationFactory(role);
        using var client = application.CreateClient();
        var scenario = await application.CreateScenarioAsync();

        var readResponse = await client.GetAsync($"/api/v1/inspections/{scenario.InspectionId}/wms-referral");
        var writeResponse = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{scenario.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-1", expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }

    [Fact]
    public async Task Referral_is_unauthorized_without_authentication()
    {
        await using var application = new TestApplicationFactory(authenticate: false);
        using var client = application.CreateClient();
        var response = await client.GetAsync($"/api/v1/inspections/{Guid.NewGuid()}/wms-referral");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Referral_validates_number_and_revision()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var gsdClient = application.CreateClient();
        var gsdScenario = await application.CreateScenarioAsync();

        var emptyNumber = await gsdClient.PutAsJsonAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/wms-referral",
            new { externalPmNumber = "   ", expectedRevision = 0 });
        var oversizedNumber = await gsdClient.PutAsJsonAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/wms-referral",
            new { externalPmNumber = new string('x', 129), expectedRevision = 0 });
        var negativeRevision = await gsdClient.PutAsJsonAsync(
            $"/api/v1/inspections/{gsdScenario.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-1", expectedRevision = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, emptyNumber.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedNumber.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativeRevision.StatusCode);
    }

    [Fact]
    public async Task Inspection_registry_filters_referral_status_and_number_without_publishing_draft_rows()
    {
        await using var application = new TestApplicationFactory(AuthRoleCatalog.Gsd);
        using var client = application.CreateClient();
        var referred = await application.CreateScenarioAsync();
        var pending = await application.CreateScenarioAsync();
        var operational = await application.CreateScenarioAsync(isOperational: true);
        var draft = await application.CreateScenarioAsync(formStatus: PreventiveMaintenanceFormStatusCatalog.Draft);
        var recorded = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{referred.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-SEARCH-77", expectedRevision = 0 });
        recorded.EnsureSuccessStatusCode();
        var draftReferral = await client.PutAsJsonAsync(
            $"/api/v1/inspections/{draft.InspectionId}/wms-referral",
            new { externalPmNumber = "WMS-PM-SEARCH-DRAFT", expectedRevision = 0 });
        Assert.Equal(HttpStatusCode.Conflict, draftReferral.StatusCode);

        var referredResponse = await client.GetAsync(
            "/api/v1/inspections?wmsReferralStatus=ReferredToWms&search=SEARCH-77");
        referredResponse.EnsureSuccessStatusCode();
        var referredRows = await referredResponse.Content.ReadFromJsonAsync<List<InspectionResponse>>();
        Assert.NotNull(referredRows);
        Assert.Equal(referred.InspectionId, Assert.Single(referredRows).Id);
        Assert.Equal("WMS-PM-SEARCH-77", referredRows[0].ExternalPmNumber);
        Assert.Equal(InspectionFollowUpStatusCatalog.ReferredToWms, referredRows[0].CorrectiveFollowUpStatus);

        var pendingResponse = await client.GetAsync(
            "/api/v1/inspections?wmsReferralStatus=CorrectiveFollowUpPending");
        pendingResponse.EnsureSuccessStatusCode();
        var pendingRows = await pendingResponse.Content.ReadFromJsonAsync<List<InspectionResponse>>();
        Assert.NotNull(pendingRows);
        Assert.Contains(pendingRows, row => row.Id == pending.InspectionId);
        Assert.DoesNotContain(pendingRows, row => row.Id == operational.InspectionId);
        Assert.DoesNotContain(pendingRows, row => row.Id == draft.InspectionId);

        var invalidStatus = await client.GetAsync("/api/v1/inspections?wmsReferralStatus=anything");
        Assert.Equal(HttpStatusCode.BadRequest, invalidStatus.StatusCode);
    }

    private sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string[] roles;
        private readonly bool authenticate;
        private readonly string databaseName = $"unipm-wms-referral-{Guid.NewGuid():N}";

        public TestApplicationFactory(params string[] roles)
            : this(authenticate: true, roles)
        {
        }

        public TestApplicationFactory(bool authenticate, params string[] roles)
        {
            this.authenticate = authenticate;
            this.roles = roles;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                if (authenticate)
                {
                    services.AddTestAuthentication(roles);
                }
                else
                {
                    var scheduleWorker = services.FirstOrDefault(descriptor =>
                        descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                        && descriptor.ImplementationType == typeof(PreventiveMaintenanceScheduleGenerationWorker));
                    if (scheduleWorker is not null)
                    {
                        services.Remove(scheduleWorker);
                    }
                }
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task<ReferralScenario> CreateScenarioAsync(
            string formStatus = PreventiveMaintenanceFormStatusCatalog.Acknowledged,
            bool includeAcknowledgement = true,
            bool isOperational = false,
            bool workCompleted = true,
            bool linkForm = true)
        {
            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = $"WMS-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                AssetCategory = "fire-extinguisher",
                Department = "GSD",
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };
            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(8)),
                PmCycle = "2026-08",
                PeriodType = "Quarter",
                Status = ScheduleStatusCatalog.Completed,
                Quarter = "Q3",
                Year = 2026,
                CompletedAt = now.AddMinutes(-1),
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
            };
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                FileNumber = $"PM-WMS-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                AssetCategory = asset.AssetCategory,
                Department = "GSD",
                PmCycle = schedule.PmCycle,
                PeriodType = schedule.PeriodType,
                Quarter = schedule.Quarter,
                Year = schedule.Year,
                Status = formStatus,
                CreatedByUserId = TestAuthenticationHandler.UserId,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
            };
            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                AssetId = asset.Id,
                PreventiveMaintenanceFormId = linkForm ? form.Id : null,
                InspectorUserId = TestAuthenticationHandler.UserId,
                DateInspected = now.AddDays(-1),
                CompletedAt = workCompleted ? now.AddMinutes(-1) : null,
                IsOperational = isOperational,
                Remarks = "Needs corrective follow-up",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddMinutes(-1)
            };

            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.PreventiveMaintenanceForms.Add(form);
            context.InspectionRecords.Add(inspection);
            if (includeAcknowledgement)
            {
                context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
                {
                    Id = Guid.NewGuid(),
                    FormId = form.Id,
                    SignatoryName = "Demo Signatory",
                    SignatoryPosition = "Department Head",
                    CapturedByUserId = TestAuthenticationHandler.UserId,
                    AcknowledgedAt = now.AddMinutes(-1)
                });
            }

            await context.SaveChangesAsync();
            return new ReferralScenario(inspection.Id, schedule.Id, form.Id);
        }

        public async Task<WorkflowState> ReadWorkflowStateAsync(ReferralScenario scenario)
        {
            await using var scope = Services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var context = await factory.CreateDbContextAsync();
            var inspection = await context.InspectionRecords.SingleAsync(row => row.Id == scenario.InspectionId);
            var schedule = await context.PreventiveMaintenanceSchedules.SingleAsync(row => row.Id == scenario.ScheduleId);
            var form = await context.PreventiveMaintenanceForms.SingleAsync(row => row.Id == scenario.FormId);
            var acknowledgement = await context.PreventiveMaintenanceAcknowledgements
                .SingleAsync(row => row.FormId == scenario.FormId);
            return new WorkflowState(
                inspection.UpdatedAt,
                inspection.CompletedAt,
                schedule.UpdatedAt,
                schedule.Status,
                form.UpdatedAt,
                form.Status,
                acknowledgement.AcknowledgedAt,
                acknowledgement.SignatoryName,
                await context.InspectionWmsReferralAudits.CountAsync(row => row.InspectionId == scenario.InspectionId));
        }
    }

    private sealed record ReferralScenario(Guid InspectionId, Guid ScheduleId, Guid FormId);

    private sealed record WorkflowState(
        DateTimeOffset InspectionUpdatedAt,
        DateTimeOffset? InspectionCompletedAt,
        DateTimeOffset ScheduleUpdatedAt,
        string ScheduleStatus,
        DateTimeOffset FormUpdatedAt,
        string FormStatus,
        DateTimeOffset AcknowledgementAt,
        string SignatoryName,
        int AuditCount);
}
