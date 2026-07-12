using UniPM.RetrievalBenchmark;
using BenchmarkProgram = UniPM.RetrievalBenchmark.Program;

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

    [Fact]
    public async Task Fused_report_preserves_rrf_metadata_component_ranks_and_limitations()
    {
        var manifest = CreateManifest();
        var fused = new DelegateBenchmarkRetrievalChannel(
            new BenchmarkChannelMetadata
            {
                RetrievalChannel = "fused",
                ResultLimit = 10,
                FusionMethod = "rrf",
                ReciprocalRankConstant = 60,
                CandidateLimit = 20,
                SemanticDegradationPolicy = "lexical-only degraded fallback"
            },
            (_, _) => Task.FromResult<IReadOnlyList<BenchmarkRetrievedResult>>(
            [
                new(
                    manifest.Queries[0].ExpectedRelevantInspectionIds[0],
                    1d / 61d + 1d / 62d,
                    LexicalRank: 1,
                    SemanticRank: 2,
                    FusionScore: 1d / 61d + 1d / 62d)
            ]));

        var report = await new BenchmarkEvaluationService()
            .RunAsync(manifest, [fused], DateTimeOffset.UnixEpoch);
        var query = report.Channels["fused"].PerQuery[0];

        Assert.Equal("1.1.0", report.BenchmarkFormatVersion);
        Assert.Equal(60, report.Channels["fused"].Metadata.ReciprocalRankConstant);
        Assert.Equal(20, report.Channels["fused"].Metadata.CandidateLimit);
        Assert.Equal(1, query.LexicalRanks.Values.Single());
        Assert.Equal(2, query.SemanticRanks.Values.Single());
        Assert.Contains(report.Limitations, limitation => limitation.Contains("RRF is applied", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Limitations, limitation => limitation.Contains("No fusion", StringComparison.Ordinal));
    }

    [Fact]
    public void Lexical_only_cli_parsing_does_not_require_embedding_configuration()
    {
        using var environment = new EnvironmentScope();
        environment.ClearEmbeddingConfiguration();

        var options = BenchmarkProgram.ParseOptions(["--channels", "lexical"]);

        Assert.Equal(["lexical"], options.Channels);
        Assert.Null(options.Embeddings);
    }

    [Fact]
    public void Fused_cli_parsing_accepts_valid_combinations_and_requires_embedding_configuration()
    {
        using var environment = new EnvironmentScope();
        environment.ClearEmbeddingConfiguration();

        Assert.Throws<InvalidOperationException>(
            () => BenchmarkProgram.ParseOptions(["--channels", "fused"]));

        environment.ConfigureEmbeddingConfiguration();
        foreach (var value in new[] { "fused", "lexical,fused", "lexical,semantic,fused", "fused,semantic" })
        {
            var options = BenchmarkProgram.ParseOptions(["--channels", value]);
            Assert.Contains("fused", options.Channels);
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

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["UNIPM_EMBEDDINGS_ENABLED"] = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_ENABLED"),
            ["UNIPM_EMBEDDINGS_PROVIDER_KEY"] = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_PROVIDER_KEY"),
            ["UNIPM_EMBEDDINGS_BASE_ADDRESS"] = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_BASE_ADDRESS"),
            ["UNIPM_EMBEDDINGS_MODEL"] = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_MODEL"),
            ["UNIPM_EMBEDDINGS_DIMENSIONS"] = Environment.GetEnvironmentVariable("UNIPM_EMBEDDINGS_DIMENSIONS")
        };

        public void ClearEmbeddingConfiguration()
        {
            foreach (var name in values.Keys)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        public void ConfigureEmbeddingConfiguration()
        {
            Environment.SetEnvironmentVariable("UNIPM_EMBEDDINGS_ENABLED", "true");
            Environment.SetEnvironmentVariable("UNIPM_EMBEDDINGS_PROVIDER_KEY", "test-provider");
            Environment.SetEnvironmentVariable("UNIPM_EMBEDDINGS_BASE_ADDRESS", "http://localhost");
            Environment.SetEnvironmentVariable("UNIPM_EMBEDDINGS_MODEL", "test-model");
            Environment.SetEnvironmentVariable("UNIPM_EMBEDDINGS_DIMENSIONS", "2");
        }

        public void Dispose()
        {
            foreach (var pair in values)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
