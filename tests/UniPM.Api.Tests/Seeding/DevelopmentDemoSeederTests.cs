extern alias DemoQrGenerator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;
using DemoQrWriter = DemoQrGenerator::UniPM.DemoQrGenerator.DemoQrWriter;

namespace UniPM.Api.Tests.Seeding;

public sealed class DevelopmentDemoSeederTests
{
    [Fact]
    public async Task Seed_creates_all_demo_scenarios_and_is_idempotent()
    {
        var factory = new TestContextFactory();
        var inspectorId = Guid.NewGuid();
        await SeedRequiredUsersAsync(factory, inspectorId, Guid.NewGuid());
        var seeder = CreateSeeder(factory);

        var first = await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        Assert.Equal(new DevelopmentDemoSeedResult(9, 9, 5, 2, 1), first);
        Assert.Equal(first, second);

        await using var context = factory.CreateDbContext();
        Assert.Equal(9, await context.Assets.CountAsync());
        Assert.Equal(9, await context.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(5, await context.InspectionRecords.CountAsync());
        Assert.Equal(2, await context.PreventiveMaintenanceForms.CountAsync());
        Assert.Equal(1, await context.PreventiveMaintenanceAcknowledgements.CountAsync());

        var scenarioAAssetIds = DevelopmentDemoCatalog.Assets
            .Where(asset => asset.Scenario == "scenario-a")
            .Select(asset => asset.Id)
            .ToHashSet();
        var schedules = await context.PreventiveMaintenanceSchedules
            .Include(schedule => schedule.Asset)
            .ToListAsync();
        Assert.Equal(9, schedules.Count);
        Assert.All(schedules, schedule =>
        {
            Assert.True(PreventiveMaintenanceCycle.TryParse(schedule.PmCycle, out _, out var month));
            Assert.True(CpmpScheduleFrequency.IsValid(schedule.Asset!.AssetCategory, month));
            Assert.Equal(PreventiveMaintenanceCycle.DeadlineForCycle(schedule.PmCycle), schedule.ScheduleDate);
        });

        var scenarioASchedules = schedules
            .Where(schedule => scenarioAAssetIds.Contains(schedule.AssetId))
            .ToList();
        Assert.Equal(3, scenarioASchedules.Count);
        Assert.All(scenarioASchedules, schedule =>
        {
            Assert.Equal("2026-11", schedule.PmCycle);
            Assert.Equal("Due", schedule.Status);
            Assert.Equal(inspectorId, schedule.AssignedToUserId);
        });
        Assert.False(await context.InspectionRecords
            .AnyAsync(inspection => scenarioAAssetIds.Contains(inspection.AssetId)));

        var submitted = await context.PreventiveMaintenanceForms
            .SingleAsync(form => form.Id == DevelopmentDemoCatalog.FormIds[0]);
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("2026-08", submitted.PmCycle);
        Assert.Null(await context.PreventiveMaintenanceAcknowledgements
            .SingleOrDefaultAsync(acknowledgement => acknowledgement.FormId == submitted.Id));

        var dashboard = new PmPeriodDashboardService(
            factory,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.FromHours(8))));
        var period = await dashboard.GetPeriodAsync(
            new PmPeriodDashboardQuery("2026-08", "fire-extinguisher", "Library", null, null, null),
            CancellationToken.None);
        Assert.Equal(3, period.Scheduled);
        Assert.Equal(3, period.Inspected);
        Assert.Equal(2, period.CompletedOnTime);
        Assert.Equal(1, period.CompletedLate);
        Assert.Equal(66.67m, period.OnTimeCompliancePercent);
        Assert.Equal(100m, period.ProgressPercent);
        Assert.Contains(period.Batches, batch => batch.FormStatus == "Submitted");

        var futurePeriod = await dashboard.GetPeriodAsync(
            new PmPeriodDashboardQuery("2026-11", "fire-extinguisher", "CCMS", null, null, null),
            CancellationToken.None);
        Assert.Equal("Future", futurePeriod.PeriodState);
        Assert.False(futurePeriod.ComplianceMeasurable);
        Assert.Null(futurePeriod.OnTimeCompliancePercent);
        Assert.Equal(0m, futurePeriod.ProgressPercent);

        var acknowledgedPeriod = await dashboard.GetPeriodAsync(
            new PmPeriodDashboardQuery("2026-06", "emergency-light", null, null, null, null),
            CancellationToken.None);
        Assert.Equal(3, acknowledgedPeriod.Scheduled);
        Assert.Equal(2, acknowledgedPeriod.Inspected);
        Assert.Equal(1, acknowledgedPeriod.CompletedOnTime);
        Assert.Equal(1, acknowledgedPeriod.CompletedLate);
        Assert.Equal(1, acknowledgedPeriod.NotCompleted);
        Assert.Equal(33.33m, acknowledgedPeriod.OnTimeCompliancePercent);
        Assert.Equal(66.67m, acknowledgedPeriod.ProgressPercent);
        Assert.Contains(acknowledgedPeriod.Batches, batch =>
            batch.Department == "STUDENT AFFAIRS OFFICE"
            && batch.Scheduled == 2
            && batch.Inspected == 2
            && batch.FormStatus == "Acknowledged");

        var acknowledged = await context.PreventiveMaintenanceForms
            .SingleAsync(form => form.Id == DevelopmentDemoCatalog.FormIds[1]);
        Assert.Equal("Acknowledged", acknowledged.Status);
        Assert.Equal("2026-06", acknowledged.PmCycle);
        Assert.Equal(2, await context.InspectionRecords.CountAsync(
            inspection => inspection.PreventiveMaintenanceFormId == acknowledged.Id));
        var onTimeEmergencyInspection = await context.InspectionRecords.SingleAsync(
            inspection => inspection.ScheduleId == DevelopmentDemoCatalog.ScheduleIds[6]);
        var lateEmergencyInspection = await context.InspectionRecords.SingleAsync(
            inspection => inspection.ScheduleId == DevelopmentDemoCatalog.ScheduleIds[7]);
        var onTimeDate = new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.FromHours(8));
        var lateDate = new DateTimeOffset(2026, 7, 3, 10, 0, 0, TimeSpan.FromHours(8));
        Assert.Equal(onTimeDate, onTimeEmergencyInspection.DateInspected);
        Assert.Equal(onTimeDate, onTimeEmergencyInspection.CompletedAt);
        Assert.Equal(lateDate, lateEmergencyInspection.DateInspected);
        Assert.Equal(lateDate, lateEmergencyInspection.CompletedAt);
        var acknowledgement = await context.PreventiveMaintenanceAcknowledgements
            .SingleAsync(item => item.FormId == acknowledged.Id);
        var submittedAt = new DateTimeOffset(2026, 7, 3, 11, 0, 0, TimeSpan.FromHours(8));
        Assert.Equal(
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(8)),
            acknowledgement.AcknowledgedAt);
        Assert.Equal(submittedAt, acknowledged.SubmittedAt);
        Assert.True(acknowledgement.AcknowledgedAt > submittedAt);

        var distinctQrValues = await context.Assets
            .Where(asset => scenarioAAssetIds.Contains(asset.Id))
            .Select(asset => asset.QrCodeValue)
            .Distinct()
            .CountAsync();
        Assert.Equal(3, distinctQrValues);
    }

    [Fact]
    public async Task Reset_removes_only_demo_data_and_is_repeatable()
    {
        var factory = new TestContextFactory();
        var inspectorId = Guid.NewGuid();
        var gsdId = Guid.NewGuid();
        await SeedRequiredUsersAsync(factory, inspectorId, gsdId);
        var seeder = CreateSeeder(factory);
        await seeder.SeedAsync();

        var unrelatedAssetId = Guid.NewGuid();
        var unrelatedFormId = Guid.NewGuid();
        await using (var context = factory.CreateDbContext())
        {
            context.Assets.Add(new Asset
            {
                Id = unrelatedAssetId,
                AssetCode = "UNRELATED-001",
                AssetCategory = "fire-extinguisher",
                QrCodeValue = "UNIPM-UNRELATED-001",
                Status = "Active"
            });
            context.PreventiveMaintenanceForms.Add(CreateEmptyScenarioADraft(unrelatedFormId, gsdId));
            await context.SaveChangesAsync();
        }

        var first = await seeder.ResetAsync();
        var second = await seeder.ResetAsync();

        Assert.Equal(new DevelopmentDemoResetResult(9, 9, 5, 2, 1), first);
        Assert.Equal(new DevelopmentDemoResetResult(0, 0, 0, 0, 0), second);
        await using var verificationContext = factory.CreateDbContext();
        Assert.NotNull(await verificationContext.Assets.FindAsync(unrelatedAssetId));
        Assert.NotNull(await verificationContext.PreventiveMaintenanceForms.FindAsync(unrelatedFormId));
        Assert.Equal(1, await verificationContext.Assets.CountAsync());
        Assert.Equal(0, await verificationContext.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(0, await verificationContext.InspectionRecords.CountAsync());
        Assert.Equal(1, await verificationContext.PreventiveMaintenanceForms.CountAsync());
    }

    [Fact]
    public async Task Reset_and_reseed_remove_an_abandoned_scenario_a_draft()
    {
        var factory = new TestContextFactory();
        var inspectorId = Guid.NewGuid();
        await SeedRequiredUsersAsync(factory, inspectorId, Guid.NewGuid());
        var seeder = CreateSeeder(factory);
        await seeder.SeedAsync();

        var abandonedFormId = Guid.NewGuid();
        await AddEmptyScenarioADraftAsync(factory, abandonedFormId, inspectorId);

        var reset = await seeder.ResetAsync();

        Assert.Equal(new DevelopmentDemoResetResult(9, 9, 5, 3, 1), reset);
        await using (var resetContext = factory.CreateDbContext())
        {
            Assert.Null(await resetContext.PreventiveMaintenanceForms.FindAsync(abandonedFormId));
        }

        await seeder.SeedAsync();
        await AssertPristineDemoStateAsync(factory, inspectorId);

        await AddEmptyScenarioADraftAsync(factory, Guid.NewGuid(), inspectorId);
        await seeder.SeedAsync();
        await AssertPristineDemoStateAsync(factory, inspectorId);
    }

    [Fact]
    public async Task Qr_generator_matches_the_unique_persisted_demo_payloads()
    {
        var factory = new TestContextFactory();
        await SeedRequiredUsersAsync(factory, Guid.NewGuid(), Guid.NewGuid());
        await CreateSeeder(factory).SeedAsync();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"unipm-demo-qr-{Guid.NewGuid():N}");

        try
        {
            var result = await DemoQrWriter.WriteAsync(outputDirectory);

            Assert.Equal(DevelopmentDemoCatalog.Assets.Count, result.PngCount);
            Assert.Equal(
                DevelopmentDemoCatalog.Assets.Count,
                DevelopmentDemoCatalog.Assets.Select(asset => asset.QrCodeValue).Distinct().Count());
            var html = await File.ReadAllTextAsync(result.IndexPath);

            await using var context = factory.CreateDbContext();
            foreach (var asset in DevelopmentDemoCatalog.Assets)
            {
                Assert.Equal(
                    1,
                    await context.Assets.CountAsync(candidate => candidate.QrCodeValue == asset.QrCodeValue));
                var pngPath = Path.Combine(outputDirectory, $"{asset.AssetCode}.png");
                Assert.True(File.Exists(pngPath));
                Assert.Equal(DemoQrWriter.CreatePng(asset.QrCodeValue), await File.ReadAllBytesAsync(pngPath));
                Assert.Contains(asset.QrCodeValue, html, StringComparison.Ordinal);
                Assert.Contains(asset.AssetCode, html, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static DevelopmentDemoSeeder CreateSeeder(TestContextFactory factory)
        => new(factory, new TestHostEnvironment(Environments.Development));

    private static async Task AddEmptyScenarioADraftAsync(
        TestContextFactory factory,
        Guid formId,
        Guid ownerId)
    {
        await using var context = factory.CreateDbContext();
        context.PreventiveMaintenanceForms.Add(CreateEmptyScenarioADraft(formId, ownerId));
        await context.SaveChangesAsync();
    }

    private static PreventiveMaintenanceForm CreateEmptyScenarioADraft(Guid formId, Guid ownerId)
    {
        var timestamp = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(8));
        return new PreventiveMaintenanceForm
        {
            Id = formId,
            AssetCategory = "fire-extinguisher",
            Building = null,
            Department = "CCMS",
            PmCycle = null,
            PeriodType = "Quarter",
            Quarter = "Q3",
            Semester = null,
            Year = 2026,
            AcademicYear = "2026-2027",
            Status = "Draft",
            CreatedByUserId = ownerId,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    private static async Task AssertPristineDemoStateAsync(
        TestContextFactory factory,
        Guid inspectorId)
    {
        await using var context = factory.CreateDbContext();
        Assert.Equal(9, await context.Assets.CountAsync());
        Assert.Equal(9, await context.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(5, await context.InspectionRecords.CountAsync());
        Assert.Equal(2, await context.PreventiveMaintenanceForms.CountAsync());
        Assert.Equal(1, await context.PreventiveMaintenanceAcknowledgements.CountAsync());
        Assert.False(await context.PreventiveMaintenanceForms.AnyAsync(form =>
            form.CreatedByUserId == inspectorId
            && form.Status == "Draft"
            && form.AssetCategory == "fire-extinguisher"
            && form.Department == "CCMS"
            && form.Year == 2026
            && form.Quarter == "Q3"));
    }

    private static async Task SeedRequiredUsersAsync(
        TestContextFactory factory,
        Guid inspectorId,
        Guid gsdId)
    {
        await using var context = factory.CreateDbContext();
        context.Users.AddRange(
            CreateUser(inspectorId, DevelopmentDemoCatalog.InspectorEmail, "Demo Inspector"),
            CreateUser(gsdId, DevelopmentDemoCatalog.GsdEmail, "Demo GSD"));
        await context.SaveChangesAsync();
    }

    private static ApplicationUser CreateUser(Guid id, string email, string displayName)
    {
        return new ApplicationUser
        {
            Id = id,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            DisplayName = displayName,
            IsActive = true
        };
    }

    private sealed class TestContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"unipm-demo-{Guid.NewGuid():N}")
                .Options;

        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset utcNow = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
