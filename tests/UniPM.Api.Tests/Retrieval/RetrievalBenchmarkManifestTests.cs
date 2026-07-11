using System.Text.Json.Nodes;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Tests.Seeding;
using UniPM.RetrievalBenchmark;

namespace UniPM.Api.Tests.Retrieval;

public sealed class RetrievalBenchmarkManifestTests
{
    [Fact]
    public async Task Manifest_loads_with_required_query_coverage()
    {
        var manifest = await LoadManifestAsync();

        Assert.Equal("1.1.0", manifest.EvaluationVersion);
        Assert.Equal("1.1.0", manifest.DatasetVersion);
        Assert.Equal(24, manifest.Queries.Count);
        Assert.Equal(
            ["english", "tagalog", "taglish"],
            manifest.Queries.Select(query => query.Language).Distinct().OrderBy(value => value));
        Assert.Equal(4, manifest.Queries.Select(query => query.AssetCategory).Distinct().Count());
        Assert.Contains(manifest.Queries, query => query.ScenarioTags.Contains("cold-start"));
        Assert.Contains(manifest.Queries, query => query.ScenarioTags.Contains("semantic-paraphrase"));
        Assert.Contains(manifest.Queries, query => query.ScenarioTags.Contains("distractor-resistance"));
        Assert.All(manifest.Queries, query => Assert.NotEmpty(query.ExpectedRelevantInspectionIds));
    }

    [Fact]
    public async Task Loader_rejects_unknown_properties_and_unsupported_versions()
    {
        var unknownPropertyPath = await CreateModifiedManifestAsync(root =>
        {
            root["queries"]!.AsArray()[0]!.AsObject()["unexpected"] = true;
        });
        var versionPath = await CreateModifiedManifestAsync(root =>
        {
            root["evaluationVersion"] = "9.9.9";
        });

        try
        {
            await Assert.ThrowsAsync<RetrievalEvaluationManifestException>(
                () => LoadManifestAsync(unknownPropertyPath));
            await Assert.ThrowsAsync<RetrievalEvaluationManifestException>(
                () => LoadManifestAsync(versionPath));
        }
        finally
        {
            File.Delete(unknownPropertyPath);
            File.Delete(versionPath);
        }
    }

    [Fact]
    public async Task Loader_rejects_missing_required_retrieval_filters()
    {
        var path = await CreateModifiedManifestAsync(root =>
        {
            root["queries"]!.AsArray()[0]!.AsObject().Remove("retrievalFilters");
        });

        try
        {
            await Assert.ThrowsAsync<RetrievalEvaluationManifestException>(
                () => LoadManifestAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Loader_rejects_duplicate_expected_ids_and_reversed_dates()
    {
        var path = await CreateModifiedManifestAsync(root =>
        {
            var query = root["queries"]!.AsArray()[0]!.AsObject();
            query["expectedRelevantInspectionIds"]!.AsArray().Add(
                query["expectedRelevantInspectionIds"]!.AsArray()[0]!.DeepClone());
            query["retrievalFilters"]!["dateFrom"] = "2026-01-01T00:00:00+08:00";
            query["retrievalFilters"]!["dateTo"] = "2025-01-01T00:00:00+08:00";
        });

        try
        {
            var exception = await Assert.ThrowsAsync<RetrievalEvaluationManifestException>(
                () => LoadManifestAsync(path));
            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reversed", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Cold_start_queries_keep_context_metadata_out_of_asset_filters()
    {
        var manifest = await LoadManifestAsync();

        var coldStartQueries = manifest.Queries.Where(query => query.ScenarioTags.Contains("cold-start")).ToArray();
        Assert.Equal(4, coldStartQueries.Length);
        Assert.All(coldStartQueries, query =>
        {
            Assert.NotNull(query.ContextAssetId);
            Assert.Null(query.RetrievalFilters.AssetId);
            Assert.Equal(query.AssetCategory, query.RetrievalFilters.AssetCategory);
        });
    }

    private static async Task<RetrievalEvaluationManifest> LoadManifestAsync(string? path = null)
    {
        var dataset = await new SyntheticMaintenanceDatasetLoader(
            new SyntheticMaintenanceSeedOptions { DatasetPath = SyntheticFixturePaths.OperationalFixture },
            new SyntheticMaintenanceDatasetValidator()).LoadAsync();
        return await new RetrievalEvaluationManifestLoader(dataset)
            .LoadAsync(path ?? SyntheticFixturePaths.EvaluationFixture);
    }

    private static async Task<string> CreateModifiedManifestAsync(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(SyntheticFixturePaths.EvaluationFixture))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"unipm-retrieval-manifest-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, root.ToJsonString());
        return path;
    }
}
