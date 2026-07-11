using System.Text.Json.Nodes;
using System.Text.Json;
using Json.Schema;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Assets;

namespace UniPM.Api.Tests.Seeding;

public sealed class SyntheticMaintenanceDatasetTests
{
    [Fact]
    public async Task Fixtures_exist_and_validate_against_their_schemas()
    {
        Assert.True(File.Exists(SyntheticFixturePaths.OperationalFixture));
        Assert.True(File.Exists(SyntheticFixturePaths.OperationalSchema));
        Assert.True(File.Exists(SyntheticFixturePaths.EvaluationFixture));
        Assert.True(File.Exists(SyntheticFixturePaths.EvaluationSchema));

        await AssertSchemaValidAsync(SyntheticFixturePaths.OperationalSchema, SyntheticFixturePaths.OperationalFixture);
        await AssertSchemaValidAsync(SyntheticFixturePaths.EvaluationSchema, SyntheticFixturePaths.EvaluationFixture);
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

    [Fact]
    public async Task Evaluation_manifest_is_complete_and_remains_test_only()
    {
        var dataset = await LoadOperationalDatasetAsync();
        var evaluation = (await LoadJsonObjectAsync(SyntheticFixturePaths.EvaluationFixture));
        var assetAnnotations = evaluation["assetAnnotations"]!.AsArray();
        var recordAnnotations = evaluation["recordAnnotations"]!.AsArray();

        Assert.Equal("1.1.0", evaluation["evaluationVersion"]!.GetValue<string>());
        Assert.Equal(dataset.DatasetVersion, evaluation["datasetVersion"]!.GetValue<string>());
        Assert.Equal(24, evaluation["queries"]!.AsArray().Count);
        Assert.Equal(4, assetAnnotations.Count);
        Assert.Equal(dataset.Inspections.Count, recordAnnotations.Count);

        var assetAnnotationEntries = assetAnnotations
            .Select(annotation =>
            (
                Id: Guid.Parse(annotation!["assetId"]!.GetValue<string>()),
                Code: annotation["assetCode"]!.GetValue<string>()))
            .ToList();
        Assert.Equal(assetAnnotationEntries.Count, assetAnnotationEntries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(assetAnnotationEntries.Count, assetAnnotationEntries.Select(entry => entry.Code).Distinct(StringComparer.Ordinal).Count());

        var operationalAssetsById = dataset.Assets.ToDictionary(asset => asset.Id);
        Assert.All(assetAnnotationEntries, entry =>
        {
            Assert.True(operationalAssetsById.TryGetValue(entry.Id, out var asset));
            Assert.Equal(asset!.AssetCode, entry.Code);
        });

        var expectedColdStartAssetIds = dataset.Assets
            .Where(asset => dataset.Schedules.Any(schedule => schedule.AssetId == asset.Id && schedule.Status == "Due"))
            .Where(asset => dataset.Inspections.All(inspection => inspection.AssetId != asset.Id))
            .Select(asset => asset.Id)
            .ToHashSet();
        Assert.True(expectedColdStartAssetIds.SetEquals(assetAnnotationEntries.Select(entry => entry.Id)));

        var recordAnnotationEntries = recordAnnotations
            .Select(annotation =>
            (
                Id: Guid.Parse(annotation!["inspectionId"]!.GetValue<string>()),
                SeedKey: annotation["inspectionSeedKey"]!.GetValue<string>()))
            .ToList();
        Assert.Equal(recordAnnotationEntries.Count, recordAnnotationEntries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(recordAnnotationEntries.Count, recordAnnotationEntries.Select(entry => entry.SeedKey).Distinct(StringComparer.Ordinal).Count());

        var operationalInspectionsById = dataset.Inspections.ToDictionary(inspection => inspection.Id);
        Assert.Equal(
            dataset.Inspections.Select(inspection => inspection.Id).ToHashSet(),
            recordAnnotationEntries.Select(entry => entry.Id).ToHashSet());
        Assert.All(recordAnnotationEntries, entry =>
        {
            Assert.True(operationalInspectionsById.TryGetValue(entry.Id, out var inspection));
            Assert.Equal(inspection!.Id, entry.Id);
            Assert.Equal(inspection.SeedKey, entry.SeedKey);
        });

        var annotationTags = recordAnnotations
            .SelectMany(annotation => annotation!["scenarioTags"]!.AsArray())
            .Select(tag => tag!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("english", annotationTags);
        Assert.Contains("tagalog", annotationTags);
        Assert.Contains("taglish", annotationTags);
        Assert.Contains("semantic-paraphrase", annotationTags);
        Assert.Contains("distractor", annotationTags);
        Assert.Contains("same-asset-history", annotationTags);
        Assert.Contains("similar-asset-fallback", annotationTags);
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

    private static async Task<JsonObject> LoadJsonObjectAsync(string path)
    {
        return JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
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
