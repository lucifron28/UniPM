extern alias DemoQrGenerator;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Retrieval;
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

        Assert.Equal(new DevelopmentDemoSeedResult(9, 9, 6, 2, 1), first);
        Assert.Equal(first, second);

        await using var context = factory.CreateDbContext();
        Assert.Equal(9, await context.Assets.CountAsync());
        Assert.Equal(9, await context.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(6, await context.InspectionRecords.CountAsync());
        Assert.Equal(2, await context.PreventiveMaintenanceForms.CountAsync());
        Assert.Equal(1, await context.PreventiveMaintenanceAcknowledgements.CountAsync());

        var scenarioAAssetIds = DevelopmentDemoCatalog.Assets
            .Where(asset => asset.Scenario == "scenario-a")
            .Select(asset => asset.Id)
            .ToHashSet();
        var scenarioASchedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => scenarioAAssetIds.Contains(schedule.AssetId))
            .ToListAsync();
        Assert.Equal(3, scenarioASchedules.Count);
        Assert.All(scenarioASchedules, schedule =>
        {
            Assert.Equal("2026-09", schedule.PmCycle);
            Assert.Equal("Due", schedule.Status);
            Assert.Equal(inspectorId, schedule.AssignedToUserId);
        });
        Assert.False(await context.InspectionRecords
            .AnyAsync(inspection => scenarioAAssetIds.Contains(inspection.AssetId)));

        var submitted = await context.PreventiveMaintenanceForms
            .SingleAsync(form => form.Id == DevelopmentDemoCatalog.FormIds[0]);
        Assert.Equal("Submitted", submitted.Status);
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
        Assert.Contains(period.Batches, batch => batch.FormStatus == "Submitted");

        var acknowledged = await context.PreventiveMaintenanceForms
            .SingleAsync(form => form.Id == DevelopmentDemoCatalog.FormIds[1]);
        Assert.Equal("Acknowledged", acknowledged.Status);
        Assert.NotNull(await context.PreventiveMaintenanceAcknowledgements
            .SingleOrDefaultAsync(acknowledgement => acknowledgement.FormId == acknowledged.Id));
        Assert.Equal(3, await context.MaintenanceSearchDocuments.CountAsync());
        Assert.Equal(0, await context.MaintenanceSearchDocumentEmbeddings.CountAsync());

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
        await SeedRequiredUsersAsync(factory, Guid.NewGuid(), Guid.NewGuid());
        var seeder = CreateSeeder(factory);
        await seeder.SeedAsync();

        var unrelatedAssetId = Guid.NewGuid();
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
            await context.SaveChangesAsync();
        }

        var first = await seeder.ResetAsync();
        var second = await seeder.ResetAsync();

        Assert.Equal(new DevelopmentDemoResetResult(9, 9, 6, 2, 1), first);
        Assert.Equal(new DevelopmentDemoResetResult(0, 0, 0, 0, 0), second);
        await using var verificationContext = factory.CreateDbContext();
        Assert.NotNull(await verificationContext.Assets.FindAsync(unrelatedAssetId));
        Assert.Equal(1, await verificationContext.Assets.CountAsync());
        Assert.Equal(0, await verificationContext.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(0, await verificationContext.InspectionRecords.CountAsync());
        Assert.Equal(0, await verificationContext.PreventiveMaintenanceForms.CountAsync());
        Assert.Equal(0, await verificationContext.MaintenanceSearchDocuments.CountAsync());
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
    {
        var lexiconLoader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions());
        var projector = new MaintenanceSearchDocumentProjector(
            factory,
            new MaintenanceIssueNormalizer(lexiconLoader));
        return new DevelopmentDemoSeeder(
            factory,
            projector,
            new TestHostEnvironment(Environments.Development));
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
