using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Models;

namespace UniPM.Api.Tests.Seeding;

public sealed class SyntheticMaintenanceSeederTests
{
    [Fact]
    public async Task Seed_populates_fixture_counts_and_is_idempotent()
    {
        var factory = new TestContextFactory();
        var seeder = CreateSeeder(factory);

        var firstResult = await seeder.SeedAsync();
        var secondResult = await seeder.SeedAsync();

        Assert.Equal(new SyntheticMaintenanceSeedResult(20, 34, 30), firstResult);
        Assert.Equal(firstResult, secondResult);

        await using var context = factory.CreateDbContext();
        Assert.Equal(20, await context.Assets.CountAsync());
        Assert.Equal(34, await context.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(30, await context.InspectionRecords.CountAsync());
        Assert.Equal(30, await context.PreventiveMaintenanceSchedules.CountAsync(schedule => schedule.Status == "Completed"));
        Assert.Equal(4, await context.PreventiveMaintenanceSchedules.CountAsync(schedule => schedule.Status == "Due"));
    }

    [Fact]
    public async Task Seed_restores_fixture_owned_values_without_creating_duplicates()
    {
        var factory = new TestContextFactory();
        var seeder = CreateSeeder(factory);
        var dataset = await LoadDatasetAsync();
        var fixtureAsset = dataset.Assets[0];

        await seeder.SeedAsync();

        await using (var context = factory.CreateDbContext())
        {
            var asset = await context.Assets.SingleAsync(candidate => candidate.Id == fixtureAsset.Id);
            asset.Building = "Modified outside the fixture";
            await context.SaveChangesAsync();
        }

        await seeder.SeedAsync();

        await using var verificationContext = factory.CreateDbContext();
        var restored = await verificationContext.Assets.SingleAsync(candidate => candidate.Id == fixtureAsset.Id);
        Assert.Equal(fixtureAsset.Building, restored.Building);
        Assert.Equal(20, await verificationContext.Assets.CountAsync());
    }

    [Fact]
    public async Task Reset_removes_only_fixture_records_and_is_repeatable()
    {
        var factory = new TestContextFactory();
        var seeder = CreateSeeder(factory);
        await seeder.SeedAsync();

        var unrelatedAssetId = Guid.NewGuid();
        await using (var context = factory.CreateDbContext())
        {
            context.Assets.Add(new Asset
            {
                Id = unrelatedAssetId,
                AssetCode = "FE-999",
                AssetCategory = "fire-extinguisher",
                QrCodeValue = "UNIPM-FIRE-EXTINGUISHER-unrelated",
                Status = "Active"
            });
            await context.SaveChangesAsync();
        }

        var firstReset = await seeder.ResetAsync();
        var secondReset = await seeder.ResetAsync();

        Assert.Equal(new SyntheticMaintenanceResetResult(20, 34, 30), firstReset);
        Assert.Equal(new SyntheticMaintenanceResetResult(0, 0, 0), secondReset);

        await using var verificationContext = factory.CreateDbContext();
        Assert.Equal(1, await verificationContext.Assets.CountAsync());
        Assert.NotNull(await verificationContext.Assets.FindAsync(unrelatedAssetId));
        Assert.Equal(0, await verificationContext.PreventiveMaintenanceSchedules.CountAsync());
        Assert.Equal(0, await verificationContext.InspectionRecords.CountAsync());
    }

    [Fact]
    public async Task Invalid_fixture_reference_fails_before_writes()
    {
        var invalidPath = await CreateModifiedFixtureAsync(root =>
        {
            root["schedules"]!.AsArray()[0]!["assetId"] = Guid.NewGuid().ToString();
        });

        try
        {
            var factory = new TestContextFactory();
            var seeder = CreateSeeder(factory, invalidPath);

            await Assert.ThrowsAsync<SyntheticMaintenanceFixtureException>(() => seeder.SeedAsync());

            await using var context = factory.CreateDbContext();
            Assert.Equal(0, await context.Assets.CountAsync());
            Assert.Equal(0, await context.PreventiveMaintenanceSchedules.CountAsync());
            Assert.Equal(0, await context.InspectionRecords.CountAsync());
        }
        finally
        {
            File.Delete(invalidPath);
        }
    }

    [Fact]
    public async Task Asset_code_and_qr_conflicts_fail_without_writes()
    {
        var dataset = await LoadDatasetAsync();
        var fixtureAsset = dataset.Assets[0];

        var codeConflictFactory = new TestContextFactory();
        await AddUnrelatedAssetAsync(codeConflictFactory, fixtureAsset.AssetCode, "UNIPM-FIRE-EXTINGUISHER-unrelated-code");
        await Assert.ThrowsAsync<SyntheticMaintenanceFixtureException>(() => CreateSeeder(codeConflictFactory).SeedAsync());

        var qrConflictFactory = new TestContextFactory();
        await AddUnrelatedAssetAsync(qrConflictFactory, "FE-998", fixtureAsset.QrCodeValue);
        await Assert.ThrowsAsync<SyntheticMaintenanceFixtureException>(() => CreateSeeder(qrConflictFactory).SeedAsync());

        await using var codeConflictContext = codeConflictFactory.CreateDbContext();
        await using var qrConflictContext = qrConflictFactory.CreateDbContext();
        Assert.Equal(1, await codeConflictContext.Assets.CountAsync());
        Assert.Equal(1, await qrConflictContext.Assets.CountAsync());
    }

    private static SyntheticMaintenanceSeeder CreateSeeder(TestContextFactory factory, string? datasetPath = null)
    {
        var validator = new SyntheticMaintenanceDatasetValidator();
        var loader = new SyntheticMaintenanceDatasetLoader(
            new SyntheticMaintenanceSeedOptions { DatasetPath = datasetPath ?? SyntheticFixturePaths.OperationalFixture },
            validator);

        return new SyntheticMaintenanceSeeder(factory, loader);
    }

    private static async Task<SyntheticMaintenanceDataset> LoadDatasetAsync()
    {
        var validator = new SyntheticMaintenanceDatasetValidator();
        var loader = new SyntheticMaintenanceDatasetLoader(
            new SyntheticMaintenanceSeedOptions { DatasetPath = SyntheticFixturePaths.OperationalFixture },
            validator);

        return await loader.LoadAsync();
    }

    private static async Task AddUnrelatedAssetAsync(TestContextFactory factory, string assetCode, string qrCodeValue)
    {
        await using var context = factory.CreateDbContext();
        context.Assets.Add(new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = assetCode,
            AssetCategory = "fire-extinguisher",
            QrCodeValue = qrCodeValue,
            Status = "Active"
        });
        await context.SaveChangesAsync();
    }

    private static async Task<string> CreateModifiedFixtureAsync(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(SyntheticFixturePaths.OperationalFixture))!.AsObject();
        mutate(root);

        var path = Path.Combine(Path.GetTempPath(), $"unipm-synthetic-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, root.ToJsonString());
        return path;
    }

    private sealed class TestContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-synthetic-{Guid.NewGuid():N}")
            .Options;

        public ApplicationDbContext CreateDbContext()
        {
            return new ApplicationDbContext(_options);
        }

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
