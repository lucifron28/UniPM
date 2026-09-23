using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmPeriodDashboardTests
{
    private const string AssetCategory = "fire-extinguisher";
    private const string PmCycle = "2026-06";

    [Fact]
    public async Task Condition_filter_does_not_change_official_scope_metrics()
    {
        var all = await GetClosedDashboardAsync(CreateQuery());
        var filtered = await GetClosedDashboardAsync(
            CreateQuery(condition: PmPeriodDashboardFilterCatalog.NonOperational));

        AssertOfficialMetricsEqual(all, filtered);
        var asset = Assert.Single(filtered.Assets);
        Assert.Equal("FE-002", asset.AssetCode);
    }

    [Fact]
    public async Task Timeliness_filter_does_not_change_official_scope_metrics()
    {
        var all = await GetClosedDashboardAsync(CreateQuery());
        var filtered = await GetClosedDashboardAsync(
            CreateQuery(timeliness: PmPeriodDashboardFilterCatalog.OnTime));

        AssertOfficialMetricsEqual(all, filtered);
        Assert.Equal(2, filtered.Assets.Count);
        Assert.All(filtered.Assets, asset => Assert.Equal(PmPeriodDashboardFilterCatalog.OnTime, asset.Timeliness));
    }

    [Fact]
    public async Task Search_filter_does_not_change_official_scope_metrics()
    {
        var all = await GetClosedDashboardAsync(CreateQuery());
        var filtered = await GetClosedDashboardAsync(CreateQuery(search: "FE-001"));

        AssertOfficialMetricsEqual(all, filtered);
        var asset = Assert.Single(filtered.Assets);
        Assert.Equal("FE-001", asset.AssetCode);
    }

    [Fact]
    public async Task Department_filter_changes_the_official_scope()
    {
        var all = await GetClosedDashboardAsync(CreateQuery());
        var ccms = await GetClosedDashboardAsync(CreateQuery(department: "CCMS"));

        Assert.Equal(5, all.Scheduled);
        Assert.Equal(4, ccms.Scheduled);
        Assert.Equal(2, all.CompletedOnTime);
        Assert.Equal(2, ccms.CompletedOnTime);
        Assert.Equal(40m, all.OnTimeCompliancePercent);
        Assert.Equal(50m, ccms.OnTimeCompliancePercent);
        Assert.Equal("CCMS", ccms.Department);
        var batch = Assert.Single(ccms.Batches);
        Assert.Equal("CCMS", batch.Department);
        Assert.Equal(4, batch.Scheduled);
        Assert.Equal(3, batch.Inspected);
        Assert.Equal(1, batch.NotCompleted);
    }

    [Fact]
    public async Task Batch_summary_uses_canonical_scope_and_inspection_source_fields()
    {
        const string otherCycle = "2026-07";
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var lateCompletion = deadline.AddTicks(1);
        var submittedAt = AtManila(2026, 6, 30);
        var formId = Guid.Parse("00000000-0000-0000-0000-000000000201");

        var response = await GetClosedDashboardAsync(
            CreateQuery(),
            context =>
            {
                var main = CreateAsset("FE-BATCH-001", "CCMS", building: "Main");
                var annex = CreateAsset("FE-BATCH-002", "CCMS", building: "South Annex");
                var otherDepartment = CreateAsset("FE-BATCH-003", "OTHER");
                var otherCategory = CreateAsset("FA-BATCH-001", "CCMS", assetCategory: "fire-alarm");
                var otherCycleAsset = CreateAsset("FE-BATCH-004", "CCMS");
                var mainSchedule = CreateSchedule(main, PmCycle);
                var annexSchedule = CreateSchedule(annex, PmCycle);
                var otherDepartmentSchedule = CreateSchedule(otherDepartment, PmCycle);
                var otherCategorySchedule = CreateSchedule(otherCategory, PmCycle);
                var otherCycleSchedule = CreateSchedule(otherCycleAsset, otherCycle);
                var form = new PreventiveMaintenanceForm
                {
                    Id = formId,
                    AssetCategory = AssetCategory,
                    Department = "CCMS",
                    PmCycle = PmCycle,
                    PeriodType = "Quarter",
                    Status = PreventiveMaintenanceFormStatusCatalog.Submitted,
                    CreatedByUserId = Guid.NewGuid(),
                    SubmittedAt = submittedAt,
                    FieldWorkCompletedAt = submittedAt.AddDays(10),
                    CreatedAt = submittedAt.AddDays(-1),
                    UpdatedAt = submittedAt
                };

                context.Assets.AddRange(main, annex, otherDepartment, otherCategory, otherCycleAsset);
                context.PreventiveMaintenanceSchedules.AddRange(
                    mainSchedule,
                    annexSchedule,
                    otherDepartmentSchedule,
                    otherCategorySchedule,
                    otherCycleSchedule);
                context.PreventiveMaintenanceForms.Add(form);
                context.InspectionRecords.AddRange(
                    CreateInspection(
                        mainSchedule,
                        main,
                        AtManila(2026, 6, 20),
                        true,
                        formId,
                        "Pressure gauge finding",
                        "Arrange a pressure check."),
                    CreateInspection(
                        annexSchedule,
                        annex,
                        lateCompletion,
                        false,
                        formId,
                        "Extinguisher finding",
                        "Replace the unit."),
                    CreateInspection(
                        otherDepartmentSchedule,
                        otherDepartment,
                        AtManila(2026, 6, 20),
                        true),
                    CreateInspection(
                        otherCategorySchedule,
                        otherCategory,
                        AtManila(2026, 6, 20),
                        true),
                    CreateInspection(
                        otherCycleSchedule,
                        otherCycleAsset,
                        AtManila(2026, 6, 20),
                        true));
            });

        Assert.Equal(3, response.Scheduled);
        Assert.Equal(3, response.Assets.Count);
        Assert.DoesNotContain(response.Assets, asset => asset.AssetCode == "FA-BATCH-001");
        Assert.DoesNotContain(response.Assets, asset => asset.AssetCode == "FE-BATCH-004");

        var ccmsBatch = Assert.Single(response.Batches, batch => batch.Department == "CCMS");
        Assert.Equal(AssetCategory, ccmsBatch.AssetCategory);
        Assert.Equal(PmCycle, ccmsBatch.PmCycle);
        Assert.Equal(2, ccmsBatch.Scheduled);
        Assert.Equal(2, ccmsBatch.Inspected);
        Assert.Equal(1, ccmsBatch.CompletedOnTime);
        Assert.Equal(50m, ccmsBatch.OnTimeCompliancePercent);
        Assert.Equal(lateCompletion, ccmsBatch.FieldWorkCompletedAt);
        Assert.Equal(formId, ccmsBatch.FormId);
        Assert.Equal("Submitted", ccmsBatch.FormStatus);
        Assert.Equal(submittedAt, ccmsBatch.SubmittedAt);

        var reviewAsset = Assert.Single(response.Assets, asset => asset.AssetCode == "FE-BATCH-001");
        Assert.Equal("South Annex", Assert.Single(response.Assets, asset => asset.AssetCode == "FE-BATCH-002").Building);
        Assert.Equal("Pressure gauge finding", reviewAsset.Remarks);
        Assert.Equal("Arrange a pressure check.", reviewAsset.ActionsRecommendations);
        Assert.Equal(formId, reviewAsset.FormId);
        Assert.NotNull(reviewAsset.InspectionId);

        var otherBatch = Assert.Single(response.Batches, batch => batch.Department == "OTHER");
        Assert.Equal(1, otherBatch.Scheduled);
        Assert.Null(otherBatch.FormId);
    }

    [Fact]
    public async Task Batch_totals_remain_complete_under_asset_display_filters()
    {
        var filtered = await GetClosedDashboardAsync(
            CreateQuery(condition: PmPeriodDashboardFilterCatalog.NonOperational));

        Assert.Single(filtered.Assets);
        var ccms = Assert.Single(filtered.Batches, batch => batch.Department == "CCMS");
        var other = Assert.Single(filtered.Batches, batch => batch.Department == "OTHER");
        Assert.Equal(4, ccms.Scheduled);
        Assert.Equal(3, ccms.Inspected);
        Assert.Equal(2, ccms.CompletedOnTime);
        Assert.Equal(1, ccms.CompletedLate);
        Assert.Equal(1, ccms.NotCompleted);
        Assert.Equal(1, other.Scheduled);
        Assert.Equal(1, other.NotCompleted);
    }

    [Fact]
    public async Task Future_period_uses_scheduled_state_and_null_compliance()
    {
        var response = await GetDashboardAsync(
            new PmPeriodDashboardQuery(
                "2026-07",
                AssetCategory,
                null,
                null,
                null,
                null),
            AtManila(2026, 6, 15),
            SeedUninspectedSchedule("2026-07", "FE-FUTURE", "CCMS"));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Future, response.PeriodState);
        Assert.False(response.ComplianceMeasurable);
        Assert.False(response.InspectionResultsAvailable);
        Assert.Null(response.OnTimeCompliancePercent);
        Assert.Equal(1, response.Scheduled);
        Assert.Equal(1, response.Remaining);
        Assert.Equal(0, response.NotCompleted);
        var asset = Assert.Single(response.Assets);
        Assert.Equal(PmPeriodDashboardFilterCatalog.Scheduled, asset.Timeliness);
        Assert.Equal(PmPeriodDashboardFilterCatalog.Scheduled, asset.ExecutionStatus);
    }

    [Fact]
    public async Task Future_completed_inspection_remains_on_time_and_exposes_results()
    {
        const string futureCycle = "2026-07";
        var completedAt = AtManila(2026, 6, 15);
        var response = await GetDashboardAsync(
            new PmPeriodDashboardQuery(
                futureCycle,
                AssetCategory,
                null,
                null,
                null,
                null),
            AtManila(2026, 6, 15),
            SeedCompletedSchedule(futureCycle, "FE-FUTURE-COMPLETED", "CCMS", completedAt));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Future, response.PeriodState);
        Assert.False(response.ComplianceMeasurable);
        Assert.True(response.InspectionResultsAvailable);
        Assert.Equal(1, response.Inspected);
        Assert.Equal(1, response.CompletedOnTime);
        Assert.Equal(1, response.Operational);
        Assert.Equal(0, response.NonOperational);
        var asset = Assert.Single(response.Assets);
        Assert.Equal(PmPeriodDashboardFilterCatalog.OnTime, asset.Timeliness);
    }

    [Fact]
    public async Task Active_unfinished_asset_uses_pending_state()
    {
        var response = await GetDashboardAsync(
            CreateQuery(),
            AtManila(2026, 6, 15),
            SeedUninspectedSchedule(PmCycle, "FE-ACTIVE", "CCMS"));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Active, response.PeriodState);
        Assert.False(response.ComplianceMeasurable);
        Assert.Null(response.OnTimeCompliancePercent);
        Assert.Equal(1, response.Remaining);
        Assert.Equal(0, response.NotCompleted);
        var asset = Assert.Single(response.Assets);
        Assert.Equal(PmPeriodDashboardFilterCatalog.Pending, asset.Timeliness);
        Assert.Equal(PmPeriodDashboardFilterCatalog.Pending, asset.ExecutionStatus);
    }

    [Fact]
    public async Task Closed_unfinished_asset_uses_not_completed_state()
    {
        var response = await GetClosedDashboardAsync(
            CreateQuery(),
            SeedUninspectedSchedule(PmCycle, "FE-CLOSED", "CCMS"));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Closed, response.PeriodState);
        Assert.True(response.ComplianceMeasurable);
        Assert.Equal(1, response.NotCompleted);
        Assert.Equal(0, response.Remaining);
        var asset = Assert.Single(response.Assets);
        Assert.Equal(PmPeriodDashboardFilterCatalog.NotCompleted, asset.Timeliness);
        Assert.Equal(PmPeriodDashboardFilterCatalog.NotCompleted, asset.ExecutionStatus);
    }

    [Fact]
    public async Task Exact_deadline_completion_is_on_time()
    {
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var response = await GetDashboardAsync(
            CreateQuery(),
            deadline,
            SeedCompletedSchedule(PmCycle, "FE-DEADLINE", "CCMS", deadline));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Active, response.PeriodState);
        Assert.False(response.ComplianceMeasurable);
        Assert.Equal(1, response.CompletedOnTime);
        Assert.Equal(0, response.CompletedLate);
        Assert.Equal(PmPeriodDashboardFilterCatalog.OnTime, Assert.Single(response.Assets).Timeliness);
    }

    [Fact]
    public async Task Completion_one_tick_after_deadline_is_late()
    {
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var late = deadline.AddTicks(1);
        var response = await GetDashboardAsync(
            CreateQuery(),
            late,
            SeedCompletedSchedule(PmCycle, "FE-LATE", "CCMS", late));

        Assert.Equal(PmPeriodDashboardPeriodStateCatalog.Closed, response.PeriodState);
        Assert.True(response.ComplianceMeasurable);
        Assert.Equal(0, response.CompletedOnTime);
        Assert.Equal(1, response.CompletedLate);
        Assert.Equal(0m, response.OnTimeCompliancePercent);
        Assert.Equal(PmPeriodDashboardFilterCatalog.Late, Assert.Single(response.Assets).Timeliness);
    }

    [Fact]
    public async Task Acknowledgement_date_never_affects_compliance()
    {
        var response = await GetClosedDashboardAsync(CreateQuery());

        var acknowledged = Assert.Single(response.Assets, asset => asset.AssetCode == "FE-001");
        Assert.True(acknowledged.IsAcknowledged);
        Assert.True(acknowledged.AcknowledgedAt > response.Deadline);
        Assert.Equal(PmPeriodDashboardFilterCatalog.OnTime, acknowledged.Timeliness);
        Assert.Equal(2, response.CompletedOnTime);
        Assert.Equal(40m, response.OnTimeCompliancePercent);
        var batch = Assert.Single(response.Batches, candidate => candidate.Department == "CCMS");
        Assert.Equal(50m, batch.OnTimeCompliancePercent);
        Assert.Equal(response.Deadline, batch.FieldWorkCompletedAt);
    }

    [Fact]
    public async Task Uninspected_scheduled_assets_remain_in_the_unfiltered_asset_table()
    {
        var response = await GetClosedDashboardAsync(CreateQuery());

        Assert.Equal(response.Scheduled, response.Assets.Count);
        Assert.Contains(response.Assets, asset => asset.AssetCode == "FE-004" && !asset.IsInspected);
        Assert.Contains(response.Assets, asset => asset.AssetCode == "FE-005" && !asset.IsInspected);
    }

    private static PmPeriodDashboardQuery CreateQuery(
        string? department = null,
        string? condition = null,
        string? timeliness = null,
        string? search = null)
    {
        return new PmPeriodDashboardQuery(
            PmCycle,
            AssetCategory,
            department,
            condition,
            timeliness,
            search);
    }

    private static Task<PmPeriodDashboardResponse> GetClosedDashboardAsync(
        PmPeriodDashboardQuery query,
        Action<ApplicationDbContext>? seed = null)
    {
        return GetDashboardAsync(
            query,
            AtManila(2026, 7, 1),
            seed ?? SeedClosedScenario);
    }

    private static async Task<PmPeriodDashboardResponse> GetDashboardAsync(
        PmPeriodDashboardQuery query,
        DateTimeOffset now,
        Action<ApplicationDbContext> seed)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-pm-dashboard-{Guid.NewGuid():N}")
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            seed(context);
            await context.SaveChangesAsync();
        }

        var service = new PmPeriodDashboardService(
            new TestContextFactory(options),
            new FixedTimeProvider(now));
        return await service.GetPeriodAsync(query, CancellationToken.None);
    }

    private static void SeedClosedScenario(ApplicationDbContext context)
    {
        var fe001 = CreateAsset("FE-001", "CCMS");
        var fe002 = CreateAsset("FE-002", "CCMS");
        var fe003 = CreateAsset("FE-003", "CCMS");
        var fe004 = CreateAsset("FE-004", "CCMS");
        var fe005 = CreateAsset("FE-005", "OTHER");
        var assets = new[] { fe001, fe002, fe003, fe004, fe005 };
        context.Assets.AddRange(assets);

        var schedule001 = CreateSchedule(fe001, PmCycle);
        var schedule002 = CreateSchedule(fe002, PmCycle);
        var schedule003 = CreateSchedule(fe003, PmCycle);
        var schedule004 = CreateSchedule(fe004, PmCycle);
        var schedule005 = CreateSchedule(fe005, PmCycle);
        context.PreventiveMaintenanceSchedules.AddRange(
            schedule001,
            schedule002,
            schedule003,
            schedule004,
            schedule005);

        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(PmCycle);
        var form = new PreventiveMaintenanceForm
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000101"),
            AssetCategory = AssetCategory,
            Department = "CCMS",
            PmCycle = PmCycle,
            PeriodType = "Quarter",
            Status = PreventiveMaintenanceFormStatusCatalog.Acknowledged,
            CreatedByUserId = Guid.NewGuid(),
            SubmittedAt = deadline.AddDays(1),
            CreatedAt = deadline.AddDays(-2),
            UpdatedAt = deadline.AddDays(1)
        };
        context.PreventiveMaintenanceForms.Add(form);
        context.PreventiveMaintenanceAcknowledgements.Add(new PreventiveMaintenanceAcknowledgement
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000102"),
            FormId = form.Id,
            Form = form,
            SignatoryName = "Synthetic Signatory",
            SignatoryPosition = "Department Head",
            CapturedByUserId = Guid.NewGuid(),
            AcknowledgedAt = deadline.AddDays(2)
        });

        context.InspectionRecords.AddRange(
            CreateInspection(schedule001, fe001, deadline, true, form.Id),
            CreateInspection(schedule002, fe002, AtManila(2026, 6, 20), false),
            CreateInspection(schedule003, fe003, deadline.AddTicks(1), true));
    }

    private static Action<ApplicationDbContext> SeedUninspectedSchedule(
        string pmCycle,
        string assetCode,
        string department)
    {
        return context =>
        {
            var asset = CreateAsset(assetCode, department);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(CreateSchedule(asset, pmCycle));
        };
    }

    private static Action<ApplicationDbContext> SeedCompletedSchedule(
        string pmCycle,
        string assetCode,
        string department,
        DateTimeOffset completedAt)
    {
        return context =>
        {
            var asset = CreateAsset(assetCode, department);
            var schedule = CreateSchedule(asset, pmCycle);
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            context.InspectionRecords.Add(CreateInspection(schedule, asset, completedAt, true));
        };
    }

    private static Asset CreateAsset(
        string assetCode,
        string department,
        string assetCategory = AssetCategory,
        string building = "Main")
    {
        return new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = assetCategory,
            Building = building,
            Department = department,
            Location = "Lobby",
            Status = "Active"
        };
    }

    private static PreventiveMaintenanceSchedule CreateSchedule(
        Asset asset,
        string pmCycle)
    {
        return new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            ScheduleDate = AtManila(2026, 6, 1),
            PmCycle = pmCycle,
            PeriodType = "Quarter",
            Quarter = "Q2",
            Status = ScheduleStatusCatalog.Due
        };
    }

    private static InspectionRecord CreateInspection(
        PreventiveMaintenanceSchedule schedule,
        Asset asset,
        DateTimeOffset completedAt,
        bool isOperational,
        Guid? formId = null,
        string? remarks = null,
        string? actionsRecommendations = null)
    {
        return new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            AssetId = asset.Id,
            InspectorUserId = Guid.NewGuid(),
            DateInspected = completedAt,
            CompletedAt = completedAt,
            DateAccomplished = completedAt,
            IsOperational = isOperational,
            PreventiveMaintenanceFormId = formId,
            Remarks = remarks,
            ActionsRecommendations = actionsRecommendations
        };
    }

    private static DateTimeOffset AtManila(int year, int month, int day)
    {
        return new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.FromHours(8));
    }

    private static void AssertOfficialMetricsEqual(
        PmPeriodDashboardResponse expected,
        PmPeriodDashboardResponse actual)
    {
        Assert.Equal(expected.PeriodState, actual.PeriodState);
        Assert.Equal(expected.ComplianceMeasurable, actual.ComplianceMeasurable);
        Assert.Equal(expected.InspectionResultsAvailable, actual.InspectionResultsAvailable);
        Assert.Equal(expected.Scheduled, actual.Scheduled);
        Assert.Equal(expected.Inspected, actual.Inspected);
        Assert.Equal(expected.CompletedOnTime, actual.CompletedOnTime);
        Assert.Equal(expected.CompletedLate, actual.CompletedLate);
        Assert.Equal(expected.NotCompleted, actual.NotCompleted);
        Assert.Equal(expected.Remaining, actual.Remaining);
        Assert.Equal(expected.Operational, actual.Operational);
        Assert.Equal(expected.NonOperational, actual.NonOperational);
        Assert.Equal(expected.OnTimeCompliancePercent, actual.OnTimeCompliancePercent);
        Assert.Equal(expected.ProgressPercent, actual.ProgressPercent);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset utcNow = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestContextFactory(
        DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
