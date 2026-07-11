using System.Diagnostics;

namespace UniPM.RetrievalBenchmark;

public sealed record BenchmarkChannelRequest(
    string QueryText,
    RetrievalFilters Filters,
    int Limit);

public interface IBenchmarkRetrievalChannel
{
    BenchmarkChannelMetadata Metadata { get; }

    Task<IReadOnlyList<BenchmarkRetrievedResult>> SearchAsync(
        BenchmarkChannelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BenchmarkEvaluationService
{
    public const int ResultLimit = 10;

    public async Task<BenchmarkReport> RunAsync(
        RetrievalEvaluationManifest manifest,
        IEnumerable<IBenchmarkRetrievalChannel> channels,
        DateTimeOffset? generatedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(channels);

        var selectedChannels = channels
            .OrderBy(channel => channel.Metadata.RetrievalChannel, StringComparer.Ordinal)
            .ToArray();
        if (selectedChannels.Length == 0)
        {
            throw new ArgumentException("At least one benchmark channel must be selected.", nameof(channels));
        }

        var report = new BenchmarkReport
        {
            EvaluationManifestVersion = manifest.EvaluationVersion,
            OperationalDatasetVersion = manifest.DatasetVersion,
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow,
            SelectedChannels = selectedChannels
                .Select(channel => channel.Metadata.RetrievalChannel)
                .ToList(),
            QueryCount = manifest.Queries.Count,
            Limitations =
            [
                "Results are measured on fictional synthetic maintenance data and do not prove production GSD performance.",
                "Timing is diagnostic only and is not a statistically valid performance comparison from one local run.",
                "No fusion, RRF, score normalization, retrieval threshold, or insufficient-evidence policy is applied."
            ]
        };

        foreach (var channel in selectedChannels)
        {
            var queryReports = new List<BenchmarkQueryReport>(manifest.Queries.Count);
            foreach (var query in manifest.Queries.OrderBy(query => query.QueryId, StringComparer.Ordinal))
            {
                var stopwatch = Stopwatch.StartNew();
                var results = await channel.SearchAsync(
                    new BenchmarkChannelRequest(query.QueryText, query.RetrievalFilters, ResultLimit),
                    cancellationToken);
                stopwatch.Stop();

                var retrievedInspectionIds = results.Select(result => result.InspectionId).ToArray();
                var expectedInspectionIds = query.ExpectedRelevantInspectionIds.ToHashSet();
                var metrics = RetrievalMetricCalculator.Calculate(retrievedInspectionIds, expectedInspectionIds);
                var expectedRanks = new Dictionary<Guid, int>();
                var rawScores = new Dictionary<Guid, double>();

                for (var index = 0; index < results.Count; index++)
                {
                    var result = results[index];
                    rawScores[result.InspectionId] = result.RawScore;
                    if (expectedInspectionIds.Contains(result.InspectionId))
                    {
                        expectedRanks[result.InspectionId] = index + 1;
                    }
                }

                queryReports.Add(new BenchmarkQueryReport
                {
                    QueryId = query.QueryId,
                    Language = query.Language,
                    AssetCategory = query.AssetCategory,
                    Metrics = metrics,
                    RetrievedInspectionIds = retrievedInspectionIds.ToList(),
                    ExpectedInspectionRanks = expectedRanks,
                    RawScores = rawScores,
                    DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds
                });
            }

            var queryLookup = manifest.Queries.ToDictionary(query => query.QueryId, StringComparer.Ordinal);
            var channelReport = new BenchmarkChannelReport
            {
                Metadata = channel.Metadata with { QueryCount = queryReports.Count },
                Overall = RetrievalMetricCalculator.Aggregate(queryReports.Select(query => query.Metrics)),
                ByLanguage = AggregateSlices(
                    queryReports,
                    query => query.Language),
                ByAssetCategory = AggregateSlices(
                    queryReports,
                    query => query.AssetCategory),
                ByScenarioTag = AggregateScenarioSlices(queryReports, queryLookup),
                PerQuery = queryReports
            };

            report.Channels[channel.Metadata.RetrievalChannel] = channelReport;
        }

        return report;
    }

    private static Dictionary<string, AggregateRetrievalMetrics> AggregateSlices(
        IEnumerable<BenchmarkQueryReport> queryReports,
        Func<BenchmarkQueryReport, string> keySelector)
    {
        return queryReports
            .GroupBy(keySelector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => RetrievalMetricCalculator.Aggregate(group.Select(query => query.Metrics)),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, AggregateRetrievalMetrics> AggregateScenarioSlices(
        IEnumerable<BenchmarkQueryReport> queryReports,
        IReadOnlyDictionary<string, RetrievalEvaluationQuery> queriesById)
    {
        return queryReports
            .SelectMany(query => queriesById[query.QueryId].ScenarioTags.Select(tag => (tag, query.Metrics)))
            .GroupBy(item => item.tag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => RetrievalMetricCalculator.Aggregate(group.Select(item => item.Metrics)),
                StringComparer.Ordinal);
    }
}

public sealed class DelegateBenchmarkRetrievalChannel(
    BenchmarkChannelMetadata metadata,
    Func<BenchmarkChannelRequest, CancellationToken, Task<IReadOnlyList<BenchmarkRetrievedResult>>> search)
    : IBenchmarkRetrievalChannel
{
    public BenchmarkChannelMetadata Metadata { get; } = metadata;

    public Task<IReadOnlyList<BenchmarkRetrievedResult>> SearchAsync(
        BenchmarkChannelRequest request,
        CancellationToken cancellationToken = default)
        => search(request, cancellationToken);
}
