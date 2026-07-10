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

    [Fact]
    public async Task Evaluation_manifest_is_complete_and_remains_test_only()
    {
        var dataset = await LoadOperationalDatasetAsync();
        var evaluation = (await LoadJsonObjectAsync(SyntheticFixturePaths.EvaluationFixture));
        var assetAnnotations = evaluation["assetAnnotations"]!.AsArray();
        var recordAnnotations = evaluation["recordAnnotations"]!.AsArray();

        Assert.Equal("1.0.0", evaluation["evaluationVersion"]!.GetValue<string>());
        Assert.Equal(dataset.DatasetVersion, evaluation["datasetVersion"]!.GetValue<string>());
        Assert.Empty(evaluation["queries"]!.AsArray());
        Assert.Equal(4, assetAnnotations.Count);
        Assert.Equal(dataset.Inspections.Count, recordAnnotations.Count);

        var coldStartAssetIds = assetAnnotations
            .Select(annotation => Guid.Parse(annotation!["assetId"]!.GetValue<string>()))
            .ToHashSet();
        Assert.All(coldStartAssetIds, assetId =>
            Assert.DoesNotContain(dataset.Inspections, inspection => inspection.AssetId == assetId));
        Assert.All(coldStartAssetIds, assetId =>
            Assert.Contains(dataset.Schedules, schedule => schedule.AssetId == assetId && schedule.Status == "Due"));

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
}
