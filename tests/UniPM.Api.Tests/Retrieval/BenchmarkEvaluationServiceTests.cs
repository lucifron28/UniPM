using UniPM.RetrievalBenchmark;

namespace UniPM.Api.Tests.Retrieval;

public sealed class BenchmarkEvaluationServiceTests
{
    [Fact]
    public async Task Evaluator_runs_lexical_only_semantic_only_and_both_channels()
    {
        var manifest = CreateManifest();
        var lexical = CreateChannel("lexical", manifest.Queries[0].ExpectedRelevantInspectionIds[0]);
        var semantic = CreateChannel("semantic", manifest.Queries[1].ExpectedRelevantInspectionIds[0]);

        var lexicalOnly = await new BenchmarkEvaluationService().RunAsync(manifest, [lexical], DateTimeOffset.UnixEpoch);
        var semanticOnly = await new BenchmarkEvaluationService().RunAsync(manifest, [semantic], DateTimeOffset.UnixEpoch);
        var both = await new BenchmarkEvaluationService().RunAsync(manifest, [semantic, lexical], DateTimeOffset.UnixEpoch);

        Assert.Equal(["lexical"], lexicalOnly.SelectedChannels);
        Assert.Equal(["semantic"], semanticOnly.SelectedChannels);
        Assert.Equal(["lexical", "semantic"], both.SelectedChannels);
        Assert.Equal(DateTimeOffset.UnixEpoch, both.GeneratedAtUtc);
    }

    [Fact]
    public async Task Evaluator_passes_only_query_text_filters_and_limit_to_channels()
    {
        var manifest = CreateManifest();
        var received = new List<BenchmarkChannelRequest>();
        var channel = new DelegateBenchmarkRetrievalChannel(
            new BenchmarkChannelMetadata { RetrievalChannel = "lexical", ResultLimit = 10 },
            (request, _) =>
            {
                received.Add(request);
                return Task.FromResult<IReadOnlyList<BenchmarkRetrievedResult>>(
                    [new BenchmarkRetrievedResult(manifest.Queries[0].ExpectedRelevantInspectionIds[0], 1)]);
            });

        await new BenchmarkEvaluationService().RunAsync(manifest, [channel], DateTimeOffset.UnixEpoch);

        var firstRequest = Assert.Single(received, request => request.QueryText == "Q001");
        Assert.Equal(manifest.Queries[0].QueryText, firstRequest.QueryText);
        Assert.Equal(manifest.Queries[0].RetrievalFilters.AssetCategory, firstRequest.Filters.AssetCategory);
        Assert.Equal(10, firstRequest.Limit);
    }

    [Fact]
    public async Task Evaluator_propagates_channel_failures_and_duplicate_results()
    {
        var manifest = CreateManifest();
        var failing = new DelegateBenchmarkRetrievalChannel(
            new BenchmarkChannelMetadata { RetrievalChannel = "semantic", ResultLimit = 10 },
            (_, _) => throw new InvalidOperationException("provider unavailable"));
        var duplicate = new DelegateBenchmarkRetrievalChannel(
            new BenchmarkChannelMetadata { RetrievalChannel = "lexical", ResultLimit = 10 },
            (request, _) => Task.FromResult<IReadOnlyList<BenchmarkRetrievedResult>>(
            [
                new(manifest.Queries[0].ExpectedRelevantInspectionIds[0], 1),
                new(manifest.Queries[0].ExpectedRelevantInspectionIds[0], 1)
            ]));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BenchmarkEvaluationService().RunAsync(manifest, [failing], DateTimeOffset.UnixEpoch));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new BenchmarkEvaluationService().RunAsync(manifest, [duplicate], DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task Report_writer_is_stable_in_order_and_excludes_secret_fields()
    {
        var manifest = CreateManifest();
        var channel = CreateChannel("lexical", manifest.Queries[0].ExpectedRelevantInspectionIds[0]);
        var report = await new BenchmarkEvaluationService().RunAsync(manifest, [channel], DateTimeOffset.UnixEpoch);
        var output = Path.Combine(Path.GetTempPath(), $"unipm-benchmark-report-{Guid.NewGuid():N}");

        try
        {
            await new BenchmarkReportWriter().WriteAsync(report, output);
            var json = await File.ReadAllTextAsync(Path.Combine(output, "retrieval-benchmark.json"));
            var markdown = await File.ReadAllTextAsync(Path.Combine(output, "retrieval-benchmark.md"));
            Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SearchText", json, StringComparison.Ordinal);
            Assert.Contains("# UniPM Retrieval Benchmark", markdown, StringComparison.Ordinal);
            Assert.True(json.IndexOf("Q001", StringComparison.Ordinal) < json.IndexOf("Q002", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static DelegateBenchmarkRetrievalChannel CreateChannel(string name, Guid relevantId)
    {
        return new DelegateBenchmarkRetrievalChannel(
            new BenchmarkChannelMetadata
            {
                RetrievalChannel = name,
                ResultLimit = BenchmarkEvaluationService.ResultLimit,
                ProviderKey = name == "semantic" ? "fake-provider" : null,
                ModelKey = name == "semantic" ? "fake-model" : null,
                Dimensions = name == "semantic" ? 2 : null,
                EmbeddingProfile = name == "semantic" ? "fake-profile" : null,
                FullTextSearchReady = name == "lexical" ? true : null
            },
            (_, _) => Task.FromResult<IReadOnlyList<BenchmarkRetrievedResult>>(
                [new BenchmarkRetrievedResult(relevantId, 1)]));
    }

    private static RetrievalEvaluationManifest CreateManifest()
    {
        return new RetrievalEvaluationManifest
        {
            EvaluationVersion = "1.1.0",
            DatasetVersion = "1.1.0",
            Queries =
            [
                CreateQuery("Q001", "fire-extinguisher", "english"),
                CreateQuery("Q002", "fire-alarm", "tagalog")
            ]
        };
    }

    private static RetrievalEvaluationQuery CreateQuery(
        string queryId,
        string category,
        string language)
    {
        return new RetrievalEvaluationQuery
        {
            QueryId = queryId,
            QueryText = queryId,
            Language = language,
            AssetCategory = category,
            RetrievalFilters = new RetrievalFilters { AssetCategory = category },
            ExpectedRelevantInspectionIds = [Guid.Parse("00000000-0000-0000-0000-000000000001")],
            ScenarioTags = ["semantic-paraphrase"],
            Notes = "test"
        };
    }
}
