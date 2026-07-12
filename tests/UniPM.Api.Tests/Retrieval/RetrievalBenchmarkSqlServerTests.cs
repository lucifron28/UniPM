using System.Text.Json;
using UniPM.Api.Features.Retrieval;
using UniPM.RetrievalBenchmark;

namespace UniPM.Api.Tests.Retrieval;

public sealed class RetrievalBenchmarkSqlServerTests
{
    [SqlServerFact]
    public async Task Lexical_benchmark_runs_over_the_complete_manifest_and_writes_safe_reports()
    {
        var output = Path.Combine(Path.GetTempPath(), $"unipm-benchmark-sql-{Guid.NewGuid():N}");

        try
        {
            var result = await new SqlServerBenchmarkRunner().RunAsync(new BenchmarkRunnerOptions
            {
                Channels = ["lexical"],
                OutputDirectory = output,
                KeepDatabase = false
            });

            Assert.True(File.Exists(result.JsonReportPath));
            Assert.True(File.Exists(result.MarkdownReportPath));

            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonReportPath));
            Assert.Equal("1.1.0", report.RootElement.GetProperty("evaluationManifestVersion").GetString());
            Assert.Equal(24, report.RootElement.GetProperty("queryCount").GetInt32());
            Assert.True(report.RootElement.GetProperty("channels").TryGetProperty("lexical", out _));

            var json = await File.ReadAllTextAsync(result.JsonReportPath);
            Assert.DoesNotContain("SearchText", json, StringComparison.Ordinal);
            Assert.DoesNotContain("VectorJson", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [SqlServerFact]
    public async Task Semantic_benchmark_runs_through_sql_pipeline_with_deterministic_provider()
    {
        var output = Path.Combine(Path.GetTempPath(), $"unipm-benchmark-semantic-sql-{Guid.NewGuid():N}");
        var embeddingService = new DeterministicEmbeddingService(
            _ => [1d, 0d],
            providerKey: "benchmark-test-provider",
            modelKey: "benchmark-test-model");

        try
        {
            var result = await new SqlServerBenchmarkRunner().RunAsync(
                new BenchmarkRunnerOptions
                {
                    Channels = ["semantic"],
                    OutputDirectory = output,
                    KeepDatabase = false,
                    Embeddings = new EmbeddingOptions
                    {
                        Enabled = true,
                        ProviderKey = "benchmark-test-provider",
                        BaseAddress = "http://localhost",
                        Model = "benchmark-test-model",
                        Dimensions = 2,
                        MaxBatchSize = 8
                    }
                },
                embeddingService);

            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonReportPath));
            Assert.True(report.RootElement.GetProperty("channels").TryGetProperty("semantic", out var semantic));
            Assert.Equal("benchmark-test-provider", semantic.GetProperty("metadata").GetProperty("providerKey").GetString());
            Assert.Contains(
                report.RootElement.GetProperty("warnings").EnumerateArray().Select(value => value.GetString()),
                warning => warning?.Contains("pipeline validation", StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [SqlServerFact]
    public async Task Fused_benchmark_uses_rrf_and_preserves_component_ranks_with_deterministic_provider()
    {
        var output = Path.Combine(Path.GetTempPath(), $"unipm-benchmark-fused-sql-{Guid.NewGuid():N}");
        var embeddingService = new DeterministicEmbeddingService(
            _ => [1d, 0d],
            providerKey: "benchmark-test-provider",
            modelKey: "benchmark-test-model");

        try
        {
            var result = await new SqlServerBenchmarkRunner().RunAsync(
                new BenchmarkRunnerOptions
                {
                    Channels = ["fused"],
                    OutputDirectory = output,
                    KeepDatabase = false,
                    Embeddings = new EmbeddingOptions
                    {
                        Enabled = true,
                        ProviderKey = "benchmark-test-provider",
                        BaseAddress = "http://localhost",
                        Model = "benchmark-test-model",
                        Dimensions = 2,
                        MaxBatchSize = 8
                    }
                },
                embeddingService);

            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(result.JsonReportPath));
            Assert.Equal("1.1.0", report.RootElement.GetProperty("benchmarkFormatVersion").GetString());
            var fused = report.RootElement.GetProperty("channels").GetProperty("fused");
            Assert.Equal("rrf", fused.GetProperty("metadata").GetProperty("fusionMethod").GetString());
            Assert.True(fused.GetProperty("perQuery")[0].GetProperty("lexicalRanks").EnumerateObject().Any());
            Assert.Contains(
                report.RootElement.GetProperty("warnings").EnumerateArray().Select(value => value.GetString()),
                warning => warning?.Contains("deterministic injected", StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
