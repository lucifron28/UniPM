using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PreventiveMaintenanceFormRegistryQueryEndpointsTests
{
    [Fact]
    public async Task List_forms_combines_status_category_department_cycle_and_metadata_search()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var target = new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            FileNumber = "PM-FILTER-001",
            AssetCategory = "fire-extinguisher",
            Building = "Science",
            Department = "CCMS",
            PmCycle = "2026-02",
            PeriodType = "Quarter",
            Quarter = "Q1",
            Year = 2026,
            Status = PreventiveMaintenanceFormStatusCatalog.Draft,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var other = new PreventiveMaintenanceForm
        {
            Id = Guid.NewGuid(),
            FileNumber = "PM-FILTER-002",
            AssetCategory = "emergency-light",
            Building = "Science",
            Department = "CCMS",
            PmCycle = "2026-06",
            PeriodType = "Semester",
            Quarter = null,
            Semester = null,
            Year = 2026,
            Status = PreventiveMaintenanceFormStatusCatalog.Draft,
            CreatedByUserId = TestAuthenticationHandler.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await application.SeedFormsAsync(target, other);

        var response = await client.GetAsync(
            "/api/v1/preventive-maintenance-forms?status=Draft&assetCategory=fire-extinguisher&department=CCMS&pmCycle=2026-02&search=PM-FILTER-001");

        response.EnsureSuccessStatusCode();
        var forms = await response.Content.ReadFromJsonAsync<PreventiveMaintenanceFormPageResponse>();
        Assert.NotNull(forms);
        Assert.Equal(1, forms.TotalCount);
        Assert.Equal(target.Id, Assert.Single(forms.Items).Id);
    }

    [Fact]
    public async Task List_forms_rejects_invalid_codes_cycles_and_oversized_filters()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var oversized = new string('x', 257);

        var statusResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?status=Returned");
        var categoryResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?assetCategory=hvac");
        var cycleResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?pmCycle=2026-13");
        var searchResponse = await client.GetAsync($"/api/v1/preventive-maintenance-forms?search={oversized}");
        var departmentResponse = await client.GetAsync($"/api/v1/preventive-maintenance-forms?department={oversized}");
        var pageResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?page=0");
        var pageSizeResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?pageSize=101");
        var offsetResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?page=2147483647&pageSize=100");
        var supervisorResponse = await client.GetAsync("/api/v1/preventive-maintenance-forms?supervisor=not-an-account");

        Assert.Equal(HttpStatusCode.BadRequest, statusResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, categoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, cycleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, searchResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, departmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pageSizeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, offsetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, supervisorResponse.StatusCode);
    }

    [Fact]
    public async Task List_forms_pages_filtered_results_in_created_time_then_id_order()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var now = DateTimeOffset.UtcNow;
        var newest = Form("PM-PAGE-003", PreventiveMaintenanceFormStatusCatalog.Draft, now, Guid.NewGuid());
        var tiedFirst = Form("PM-PAGE-001", PreventiveMaintenanceFormStatusCatalog.Draft, now.AddMinutes(-1), Guid.NewGuid());
        var tiedSecond = Form("PM-PAGE-002", PreventiveMaintenanceFormStatusCatalog.Draft, now.AddMinutes(-1), Guid.NewGuid());
        tiedFirst.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        tiedSecond.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var submitted = Form("PM-PAGE-004", PreventiveMaintenanceFormStatusCatalog.Submitted, now.AddMinutes(1), Guid.NewGuid());
        await application.SeedFormsAsync(newest, tiedFirst, tiedSecond, submitted);

        var firstPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?status=Draft&page=1&pageSize=2");
        var secondPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?status=Draft&page=2&pageSize=2");
        var beyondPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?status=Draft&page=3&pageSize=2");
        var emptyFilter = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?status=Acknowledged");

        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(newest.Id, firstPage.Items[0].Id);
        Assert.Equal(tiedFirst.Id, firstPage.Items[1].Id);
        Assert.NotNull(secondPage);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal(tiedSecond.Id, Assert.Single(secondPage.Items).Id);
        Assert.NotNull(beyondPage);
        Assert.Equal(3, beyondPage.Page);
        Assert.Equal(3, beyondPage.TotalCount);
        Assert.Empty(beyondPage.Items);
        Assert.NotNull(emptyFilter);
        Assert.Equal(0, emptyFilter.TotalCount);
        Assert.Empty(emptyFilter.Items);
    }

    [Fact]
    public async Task Form_options_are_scalar_distinct_values_from_readable_forms()
    {
        await using var application = new TestApplicationFactory();
        using var client = application.CreateClient();
        var owner = TestAuthenticationHandler.UserId;
        await application.SeedFormsAsync(
            Form("PM-OPTION-001", PreventiveMaintenanceFormStatusCatalog.Draft, DateTimeOffset.UtcNow, owner,
                category: "fire-extinguisher", department: "GSD", pmCycle: "2026-02"),
            Form("PM-OPTION-002", PreventiveMaintenanceFormStatusCatalog.Submitted, DateTimeOffset.UtcNow, owner,
                category: "fire-extinguisher", department: "GSD", pmCycle: "2026-02"),
            Form("PM-OPTION-003", PreventiveMaintenanceFormStatusCatalog.Draft, DateTimeOffset.UtcNow, owner,
                category: "fire-alarm", department: null, pmCycle: null));

        var options = await client.GetFromJsonAsync<PreventiveMaintenanceFormOptionsResponse>(
            "/api/v1/preventive-maintenance-forms/options");
        var unassignedForms = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?supervisor=unassigned");

        Assert.NotNull(options);
        Assert.Equal(3, options.TotalCount);
        Assert.Equal(["fire-alarm", "fire-extinguisher"], options.Categories);
        Assert.Equal(["GSD"], options.Departments);
        Assert.Equal(["2026-02"], options.PmCycles);
        Assert.NotNull(unassignedForms);
        Assert.Equal(0, unassignedForms.TotalCount);
        Assert.Empty(unassignedForms.Items);
    }

    [Fact]
    public async Task Supervisor_filter_counts_only_forms_in_the_existing_read_scope()
    {
        await using var application = new TestApplicationFactory(true, AuthRoleCatalog.Supervisor);
        using var client = application.CreateClient();
        var fixture = await application.SeedSupervisorScenarioAsync();

        var scopedPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?page=1&pageSize=1");
        var unassignedPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            "/api/v1/preventive-maintenance-forms?supervisor=unassigned&page=1&pageSize=1");
        var otherSupervisorPage = await client.GetFromJsonAsync<PreventiveMaintenanceFormPageResponse>(
            $"/api/v1/preventive-maintenance-forms?supervisor={fixture.OtherSupervisorId}");
        var options = await client.GetFromJsonAsync<PreventiveMaintenanceFormOptionsResponse>(
            "/api/v1/preventive-maintenance-forms/options");

        Assert.NotNull(scopedPage);
        Assert.Equal(2, scopedPage.TotalCount);
        Assert.Single(scopedPage.Items);
        Assert.NotNull(unassignedPage);
        Assert.Equal(1, unassignedPage.TotalCount);
        var sharedForm = Assert.Single(unassignedPage.Items);
        Assert.Equal(fixture.SharedFormId, sharedForm.Id);
        Assert.Contains(sharedForm.Inspections, row => row.CurrentScheduleSupervisorUserId is null);
        Assert.NotNull(otherSupervisorPage);
        Assert.Equal(0, otherSupervisorPage.TotalCount);
        Assert.Empty(otherSupervisorPage.Items);
        Assert.NotNull(options);
        Assert.Equal(2, options.TotalCount);
        Assert.DoesNotContain("emergency-light", options.Categories);
    }

    private static PreventiveMaintenanceForm Form(
        string fileNumber,
        string status,
        DateTimeOffset createdAt,
        Guid creatorId,
        string category = "fire-extinguisher",
        string? department = "CCMS",
        string? pmCycle = "2026-02") => new()
    {
        Id = Guid.NewGuid(),
        FileNumber = fileNumber,
        AssetCategory = category,
        Department = department,
        PmCycle = pmCycle,
        PeriodType = "Quarter",
        Quarter = "Q1",
        Year = 2026,
        Status = status,
        CreatedByUserId = creatorId,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    private sealed class TestApplicationFactory(bool authenticated = true, params string[] roles) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"unipm-form-registry-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                if (authenticated)
                {
                    services.AddTestAuthentication(roles.Length == 0 ? [AuthRoleCatalog.Gsd] : roles);
                }
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }

        public async Task SeedFormsAsync(params PreventiveMaintenanceForm[] forms)
        {
            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.PreventiveMaintenanceForms.AddRange(forms);
            await context.SaveChangesAsync();
        }

        public async Task<SupervisorFixture> SeedSupervisorScenarioAsync()
        {
            var actorId = TestAuthenticationHandler.UserId;
            var otherSupervisorId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var sharedForm = Form("PM-SCOPE-SHARED", PreventiveMaintenanceFormStatusCatalog.Draft,
                now.AddMinutes(-1), Guid.NewGuid(), "fire-extinguisher", "GSD", "2026-02");
            var assignedForm = Form("PM-SCOPE-ASSIGNED", PreventiveMaintenanceFormStatusCatalog.Draft,
                now, Guid.NewGuid(), "fire-alarm", "GSD", "2026-05");
            var unrelatedForm = Form("PM-SCOPE-OTHER", PreventiveMaintenanceFormStatusCatalog.Draft,
                now.AddMinutes(1), Guid.NewGuid(), "emergency-light", "Hidden", "2026-08");
            var sharedAsset = Asset("PM-SCOPE-SHARED-ASSET", "fire-extinguisher");
            var sharedUnassignedAsset = Asset("PM-SCOPE-SHARED-UNASSIGNED-ASSET", "fire-extinguisher");
            var assignedAsset = Asset("PM-SCOPE-ASSIGNED-ASSET", "fire-alarm");
            var unrelatedAsset = Asset("PM-SCOPE-OTHER-ASSET", "emergency-light");
            var sharedSupervisorSchedule = Schedule(sharedAsset, actorId);
            var unassignedSchedule = Schedule(sharedUnassignedAsset, null);
            var assignedSchedule = Schedule(assignedAsset, actorId);
            var unrelatedSchedule = Schedule(unrelatedAsset, otherSupervisorId);
            var inspections = new[]
            {
                Inspection(sharedForm, sharedAsset, sharedSupervisorSchedule),
                Inspection(sharedForm, sharedUnassignedAsset, unassignedSchedule),
                Inspection(assignedForm, assignedAsset, assignedSchedule),
                Inspection(unrelatedForm, unrelatedAsset, unrelatedSchedule)
            };

            await using var context = await Services
                .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
                .CreateDbContextAsync();
            context.Assets.AddRange(sharedAsset, sharedUnassignedAsset, assignedAsset, unrelatedAsset);
            context.PreventiveMaintenanceSchedules.AddRange(
                sharedSupervisorSchedule, unassignedSchedule, assignedSchedule, unrelatedSchedule);
            context.PreventiveMaintenanceForms.AddRange(sharedForm, assignedForm, unrelatedForm);
            context.InspectionRecords.AddRange(inspections);
            await context.SaveChangesAsync();

            return new SupervisorFixture(sharedForm.Id, otherSupervisorId);
        }

        private static Asset Asset(string assetCode, string category) => new()
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = category,
            Department = "GSD",
            Status = "Active"
        };

        private static PreventiveMaintenanceSchedule Schedule(Asset asset, Guid? supervisorId) => new()
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            ScheduleDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PmCycle = "2026-02",
            PeriodType = "Quarter",
            Quarter = "Q1",
            Year = 2026,
            Status = "Due",
            AssignedSupervisorUserId = supervisorId
        };

        private static InspectionRecord Inspection(
            PreventiveMaintenanceForm form,
            Asset asset,
            PreventiveMaintenanceSchedule? schedule) => new()
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule?.Id ?? Guid.NewGuid(),
            Schedule = schedule,
            AssetId = asset.Id,
            Asset = asset,
            PreventiveMaintenanceFormId = form.Id,
            PreventiveMaintenanceForm = form,
            InspectorUserId = Guid.NewGuid(),
            DateInspected = DateTimeOffset.UtcNow,
            IsOperational = true
        };
    }

    private sealed record SupervisorFixture(Guid SharedFormId, Guid OtherSupervisorId);
}
