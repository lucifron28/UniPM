using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;
using Xunit.Abstractions;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3BoundedScaleTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset AsOf = new(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public async Task Synthetic_report_scales_are_measured_and_50000_row_limit_is_enforced()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PmAnalyticsV3Scale_{Guid.NewGuid():N}")
            .Options;
        await using (var context = new ApplicationDbContext(options))
        {
            SeedScale(context, "fire-extinguisher", 100, "2025-02", "FE");
            SeedScale(context, "fire-alarm", 1_000, "2025-06", "FA");
            SeedScale(context, "emergency-light", 10_000, "2025-06", "EL");
            SeedScale(context, "water-drinking-station", 50_000, "2025-02", "WDS");
            await context.SaveChangesAsync();
        }

        var service = new PmAnalyticsV3QueryService(new TestContextFactory(options), new FixedTimeProvider(AsOf));
        foreach (var sample in new[]
        {
            (Category: "fire-extinguisher", ExpectedRows: 100),
            (Category: "fire-alarm", ExpectedRows: 1_000),
            (Category: "emergency-light", ExpectedRows: 10_000),
            (Category: "water-drinking-station", ExpectedRows: 50_000)
        })
        {
            var inventoryElapsed = Stopwatch.StartNew();
            var inventory = await ExecuteAsync(service, CreateInventoryPlan(sample.Category));
            inventoryElapsed.Stop();
            Assert.Equal(sample.ExpectedRows, inventory.Population.SourceTotalCount);

            var executionElapsed = Stopwatch.StartNew();
            var execution = await ExecuteAsync(service, CreateExecutionPlan(sample.Category));
            executionElapsed.Stop();
            Assert.Equal(sample.ExpectedRows, execution.Population.EligibleAssetCycleCount);
            var scheduled = Assert.Single(execution.AggregateRows).Measures[PmAnalyticsV3Catalog.ScheduledCount];
            Assert.Equal(sample.ExpectedRows, scheduled.Numerator);
            output.WriteLine(
                $"synthetic report scale category={sample.Category} eligibleRows={sample.ExpectedRows} inventoryElapsedMs={inventoryElapsed.Elapsed.TotalMilliseconds:F2} pmExecutionElapsedMs={executionElapsed.Elapsed.TotalMilliseconds:F2}");
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var asset = CreateAsset("WDS", 50_000, "water-drinking-station");
            var schedule = CreateSchedule(asset, "2025-02");
            context.Assets.Add(asset);
            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<PmAnalyticsV3ExecutionLimitException>(
            () => ExecuteAsync(service, CreateInventoryPlan("water-drinking-station")));
        await Assert.ThrowsAsync<PmAnalyticsV3ExecutionLimitException>(
            () => ExecuteAsync(service, CreateExecutionPlan("water-drinking-station")));
        output.WriteLine("synthetic report boundary rows=50001 inventory=Rejected pmExecution=Rejected");
    }

    private static async Task<PmAnalyticsV3ExecutionResponse> ExecuteAsync(
        PmAnalyticsV3QueryService service,
        PmAnalyticsV3ReportPlan plan)
    {
        var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
        Assert.NotNull(resolution.Plan);
        return await service.ExecuteAsync(resolution.Plan, 1, 25, CancellationToken.None);
    }

    private static PmAnalyticsV3ReportPlan CreateInventoryPlan(string category) => new(
        3,
        PmAnalyticsV3Catalog.Version,
        PmAnalyticsV3Catalog.SubjectAssetInventory,
        PmAnalyticsV3Mode.Aggregate,
        null,
        [category],
        [PmAnalyticsV3Catalog.RegisteredAssetCount],
        [],
        [],
        [],
        [],
        [],
        null,
        PmAnalyticsV3Presentation.Table,
        [PmAnalyticsV3Catalog.RegisteredAssetCount]);

    private static PmAnalyticsV3ReportPlan CreateExecutionPlan(string category) => new(
        3,
        PmAnalyticsV3Catalog.Version,
        PmAnalyticsV3Catalog.SubjectPmExecution,
        PmAnalyticsV3Mode.Aggregate,
        new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2025),
        [category],
        [PmAnalyticsV3Catalog.ScheduledCount],
        [],
        [],
        [],
        [],
        [],
        null,
        PmAnalyticsV3Presentation.Table,
        [PmAnalyticsV3Catalog.ScheduledCount]);

    private static void SeedScale(
        ApplicationDbContext context,
        string category,
        int count,
        string cycle,
        string prefix)
    {
        var year = int.Parse(cycle[..4], System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(cycle[^2..], System.Globalization.CultureInfo.InvariantCulture);
        var assets = new List<Asset>(count);
        var schedules = new List<PreventiveMaintenanceSchedule>(count);
        for (var index = 0; index < count; index++)
        {
            var asset = CreateAsset(prefix, index, category);
            assets.Add(asset);
            schedules.Add(CreateSchedule(asset, cycle, year, month));
        }

        context.Assets.AddRange(assets);
        context.PreventiveMaintenanceSchedules.AddRange(schedules);
    }

    private static Asset CreateAsset(string prefix, int index, string category) => new()
    {
        Id = Guid.NewGuid(),
        AssetCode = $"SCALE-{prefix}-{index:D6}",
        AssetCategory = category,
        Department = "Synthetic Scale Department",
        Building = "Synthetic Scale Building",
        Location = "Synthetic Scale Location",
        Status = AssetStatusCatalog.Active
    };

    private static PreventiveMaintenanceSchedule CreateSchedule(
        Asset asset,
        string cycle,
        int? year = null,
        int? month = null) => new()
    {
        Id = Guid.NewGuid(),
        AssetId = asset.Id,
        Asset = asset,
        PmCycle = cycle,
        ScheduleDate = year is { } scheduleYear && month is { } scheduleMonth
            ? new DateTimeOffset(scheduleYear, scheduleMonth, 1, 0, 0, 0, TimeSpan.FromHours(8))
            : PreventiveMaintenanceCycle.DeadlineForCycle(cycle),
        PeriodType = "Quarter",
        Status = ScheduleStatusCatalog.Due,
        Quarter = "Q1",
        Year = year ?? int.Parse(cycle[..4], System.Globalization.CultureInfo.InvariantCulture)
    };

    private sealed class TestContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
