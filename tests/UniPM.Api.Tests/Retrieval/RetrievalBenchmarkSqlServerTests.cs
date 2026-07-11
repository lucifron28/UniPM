using System.Text.Json;
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
}
