using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniPM.RetrievalBenchmark;

public sealed class BenchmarkReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict
    };

    public async Task WriteAsync(
        BenchmarkReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "retrieval-benchmark.json");
        var markdownPath = Path.Combine(outputDirectory, "retrieval-benchmark.md");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            BuildMarkdown(report),
            cancellationToken);
    }

    private static string BuildMarkdown(BenchmarkReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# UniPM Retrieval Benchmark");
        builder.AppendLine();
        builder.AppendLine($"- Evaluation manifest: `{report.EvaluationManifestVersion}`");
        builder.AppendLine($"- Operational dataset: `{report.OperationalDatasetVersion}`");
        builder.AppendLine($"- Generated at UTC: `{report.GeneratedAtUtc:O}`");
        builder.AppendLine($"- Queries: `{report.QueryCount}`");
        builder.AppendLine($"- Channels: `{string.Join(", ", report.SelectedChannels)}`");
        builder.AppendLine();
        builder.AppendLine("> Synthetic benchmark results are pipeline evidence only and do not prove production GSD performance.");
        builder.AppendLine();

        foreach (var channel in report.Channels.OrderBy(channel => channel.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"## {channel.Key}");
            builder.AppendLine();
            builder.AppendLine($"Result limit: `{channel.Value.Metadata.ResultLimit}`; queries: `{channel.Value.Metadata.QueryCount}`");
            builder.AppendLine();
            AppendMetricTable(builder, "Overall", new[] { ("overall", channel.Value.Overall) });
            AppendMetricTable(builder, "By language", channel.Value.ByLanguage);
            AppendMetricTable(builder, "By asset category", channel.Value.ByAssetCategory);
            AppendMetricTable(builder, "By scenario tag", channel.Value.ByScenarioTag);

            builder.AppendLine("### Weakest queries");
            builder.AppendLine();
            foreach (var query in channel.Value.PerQuery
                         .OrderBy(query => query.Metrics.ReciprocalRank)
                         .ThenBy(query => query.QueryId, StringComparer.Ordinal)
                         .Take(5))
            {
                builder.AppendLine(
                    $"- `{query.QueryId}` (`{FormatQueryText(query.QueryText)}`; {query.AssetCategory}, {query.Language}): MRR `{Format(query.Metrics.ReciprocalRank)}`, Recall@5 `{Format(query.Metrics.RecallAt5)}`");
            }

            builder.AppendLine();
        }

        AppendChannelComparison(builder, report);
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations.OrderBy(value => value, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {limitation}");
        }

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var warning in report.Warnings.OrderBy(value => value, StringComparer.Ordinal))
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }

    private static void AppendMetricTable(
        StringBuilder builder,
        string title,
        IEnumerable<(string Key, AggregateRetrievalMetrics Metrics)> metrics)
    {
        builder.AppendLine($"### {title}");
        builder.AppendLine();
        builder.AppendLine("| Slice | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var (key, value) in metrics.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| `{key}` | {Format(value.HitAt1)} | {Format(value.HitAt5)} | {Format(value.PrecisionAt5)} | {Format(value.RecallAt5)} | {Format(value.RecallAt10)} | {Format(value.MeanReciprocalRank)} |");
        }

        builder.AppendLine();
    }

    private static void AppendMetricTable(
        StringBuilder builder,
        string title,
        IReadOnlyDictionary<string, AggregateRetrievalMetrics> metrics)
    {
        AppendMetricTable(builder, title, metrics.Select(item => (item.Key, item.Value)));
    }

    private static void AppendChannelComparison(StringBuilder builder, BenchmarkReport report)
    {
        if (!report.Channels.ContainsKey("lexical") || !report.Channels.ContainsKey("semantic"))
        {
            return;
        }

        var lexical = report.Channels["lexical"].PerQuery.ToDictionary(query => query.QueryId, StringComparer.Ordinal);
        var semantic = report.Channels["semantic"].PerQuery.ToDictionary(query => query.QueryId, StringComparer.Ordinal);

        builder.AppendLine("## Channel comparison");
        builder.AppendLine();
        builder.AppendLine("The comparison below uses per-query reciprocal rank only; scores are not normalized or fused.");
        builder.AppendLine();
        builder.AppendLine("### Lexical outperformed semantic");
        builder.AppendLine();
        foreach (var queryId in lexical.Keys.Intersect(semantic.Keys, StringComparer.Ordinal)
                     .Where(queryId => lexical[queryId].Metrics.ReciprocalRank > semantic[queryId].Metrics.ReciprocalRank)
                     .OrderBy(queryId => queryId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{queryId}` (`{FormatQueryText(lexical[queryId].QueryText)}`)");
        }

        builder.AppendLine();
        builder.AppendLine("### Semantic outperformed lexical");
        builder.AppendLine();
        foreach (var queryId in lexical.Keys.Intersect(semantic.Keys, StringComparer.Ordinal)
                     .Where(queryId => semantic[queryId].Metrics.ReciprocalRank > lexical[queryId].Metrics.ReciprocalRank)
                     .OrderBy(queryId => queryId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{queryId}` (`{FormatQueryText(semantic[queryId].QueryText)}`)");
        }

        builder.AppendLine();
        builder.AppendLine("### Neither channel found a relevant result");
        builder.AppendLine();
        foreach (var queryId in lexical.Keys.Intersect(semantic.Keys, StringComparer.Ordinal)
                     .Where(queryId => lexical[queryId].Metrics.ReciprocalRank == 0d
                         && semantic[queryId].Metrics.ReciprocalRank == 0d)
                     .OrderBy(queryId => queryId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- `{queryId}` (`{FormatQueryText(lexical[queryId].QueryText)}`)");
        }

        builder.AppendLine();
    }

    private static string Format(double value)
        => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatQueryText(string value)
        => value.Replace("`", "'", StringComparison.Ordinal);
}
