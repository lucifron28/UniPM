using System.Text.Json.Nodes;
using System.Text.Json;
using Json.Schema;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Assets;

namespace UniPM.Api.Tests.Seeding;

public sealed class SyntheticMaintenanceDatasetTests
{
    [Fact]
    public async Task Operational_fixture_exists_and_validates_against_its_schema()
    {
        Assert.True(File.Exists(SyntheticFixturePaths.OperationalFixture));
        Assert.True(File.Exists(SyntheticFixturePaths.OperationalSchema));

        await AssertSchemaValidAsync(SyntheticFixturePaths.OperationalSchema, SyntheticFixturePaths.OperationalFixture);
    }

    [Fact]
    public async Task Operational_fixture_is_valid_and_contains_only_operational_data()
    {
        var dataset = await LoadOperationalDatasetAsync();
        var operationalJson = await File.ReadAllTextAsync(SyntheticFixturePaths.OperationalFixture);

        Assert.Equal("1.1.0", dataset.DatasetVersion);
        Assert.Equal(5, dataset.ExpectedCounts.Actors);
        Assert.Equal(20, dataset.ExpectedCounts.Assets);
        Assert.Equal(34, dataset.ExpectedCounts.Schedules);
        Assert.Equal(30, dataset.ExpectedCounts.Inspections);
        Assert.Equal(dataset.ExpectedCounts.Actors, dataset.Actors.Count);
        Assert.Equal(dataset.ExpectedCounts.Assets, dataset.Assets.Count);
        Assert.Equal(dataset.ExpectedCounts.Schedules, dataset.Schedules.Count);
        Assert.Equal(dataset.ExpectedCounts.Inspections, dataset.Inspections.Count);

        Assert.Equal(dataset.Assets.Count, dataset.Assets.Select(asset => asset.Id).Distinct().Count());
        Assert.Equal(dataset.Schedules.Count, dataset.Schedules.Select(schedule => schedule.Id).Distinct().Count());
        Assert.Equal(dataset.Inspections.Count, dataset.Inspections.Select(inspection => inspection.Id).Distinct().Count());
        Assert.Equal(dataset.Assets.Count, dataset.Assets.Select(asset => asset.AssetCode).Distinct().Count());
        Assert.Equal(dataset.Assets.Count, dataset.Assets.Select(asset => asset.QrCodeValue).Distinct().Count());

        Assert.All(dataset.Assets, asset =>
            Assert.Equal(AssetQrCodeValue.Create(asset.AssetCategory, asset.Id), asset.QrCodeValue));

        Assert.DoesNotContain("expectedIssueKeys", operationalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("scenarioTags", operationalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("isColdStartAsset", operationalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("coldStartAssets", operationalJson, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(SyntheticAsset).GetProperties(), property => property.Name == "IsColdStartAsset");
        Assert.DoesNotContain(typeof(SyntheticInspection).GetProperties(), property => property.Name is "ExpectedIssueKeys" or "ScenarioTags");

        var categories = dataset.Assets.Select(asset => asset.AssetCategory).ToHashSet(StringComparer.Ordinal);
        Assert.True(categories.SetEquals([
            "fire-extinguisher",
            "fire-alarm",
            "emergency-light",
            "water-drinking-station"
        ]));

        Assert.All(dataset.Schedules.Where(schedule => schedule.Status == "Completed"), schedule =>
            Assert.Single(dataset.Inspections, inspection => inspection.ScheduleId == schedule.Id));
        Assert.All(dataset.Schedules.Where(schedule => schedule.Status == "Due"), schedule =>
            Assert.DoesNotContain(dataset.Inspections, inspection => inspection.ScheduleId == schedule.Id));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("asset")]
    [InlineData("inspection")]
    [InlineData("expectedIssueKeys")]
    [InlineData("scenarioTags")]
    public async Task Loader_rejects_unmapped_fixture_properties(string propertyKind)
    {
        var fixturePath = await CreateModifiedFixtureAsync(root =>
        {
            switch (propertyKind)
            {
                case "root":
                    root["unknownRootProperty"] = true;
                    break;
                case "asset":
                    root["assets"]!.AsArray()[0]!.AsObject()["unknownAssetProperty"] = true;
                    break;
                case "inspection":
                    root["inspections"]!.AsArray()[0]!.AsObject()["unknownInspectionProperty"] = true;
                    break;
                case "expectedIssueKeys":
                    root["assets"]!.AsArray()[0]!.AsObject()["expectedIssueKeys"] = new JsonArray();
                    break;
                case "scenarioTags":
                    root["inspections"]!.AsArray()[0]!.AsObject()["scenarioTags"] = new JsonArray();
                    break;
            }
        });

        try
        {
            var loader = new SyntheticMaintenanceDatasetLoader(
                new SyntheticMaintenanceSeedOptions { DatasetPath = fixturePath },
                new SyntheticMaintenanceDatasetValidator());

            await Assert.ThrowsAsync<JsonException>(() => loader.LoadAsync());
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    [Theory]
    [InlineData("assetCode", "asset codes")]
    [InlineData("qrCodeValue", "QR values")]
    [InlineData("seedKey", "seed keys")]
    public async Task Loader_rejects_case_insensitive_duplicate_strings(string propertyKind, string errorLabel)
    {
        var fixturePath = await CreateModifiedFixtureAsync(root =>
        {
            var assets = root["assets"]!.AsArray();
            var firstAsset = assets[0]!.AsObject();
            var secondAsset = assets[1]!.AsObject();

            switch (propertyKind)
            {
                case "assetCode":
                    secondAsset["assetCode"] = firstAsset["assetCode"]!.GetValue<string>().ToLowerInvariant();
                    break;
                case "qrCodeValue":
                    secondAsset["qrCodeValue"] = firstAsset["qrCodeValue"]!.GetValue<string>().ToLowerInvariant();
                    break;
                case "seedKey":
                    secondAsset["seedKey"] = firstAsset["seedKey"]!.GetValue<string>().ToUpperInvariant();
                    break;
            }
        });

        try
        {
            var loader = new SyntheticMaintenanceDatasetLoader(
                new SyntheticMaintenanceSeedOptions { DatasetPath = fixturePath },
                new SyntheticMaintenanceDatasetValidator());

            var exception = await Assert.ThrowsAsync<SyntheticMaintenanceFixtureException>(() => loader.LoadAsync());
            Assert.Contains(errorLabel, exception.Message);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    private static async Task<SyntheticMaintenanceDataset> LoadOperationalDatasetAsync()
    {
        var validator = new SyntheticMaintenanceDatasetValidator();
        var loader = new SyntheticMaintenanceDatasetLoader(
            new SyntheticMaintenanceSeedOptions { DatasetPath = SyntheticFixturePaths.OperationalFixture },
            validator);

        return await loader.LoadAsync();
    }

    private static async Task AssertSchemaValidAsync(string schemaPath, string instancePath)
    {
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(schemaPath));
        using var instance = JsonDocument.Parse(await File.ReadAllTextAsync(instancePath));
        var evaluation = schema.Evaluate(instance.RootElement);

        Assert.True(evaluation.IsValid, $"Schema validation failed for '{Path.GetFileName(instancePath)}'.");
    }

    private static async Task<string> CreateModifiedFixtureAsync(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(SyntheticFixturePaths.OperationalFixture))!.AsObject();
        mutate(root);

        var path = Path.Combine(Path.GetTempPath(), $"unipm-synthetic-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, root.ToJsonString());
        return path;
    }
}
