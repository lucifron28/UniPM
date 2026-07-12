namespace UniPM.RetrievalBenchmark;

public sealed record RetrievalMetrics(
    double HitAt1,
    double HitAt5,
    double PrecisionAt5,
    double RecallAt5,
    double RecallAt10,
    double ReciprocalRank,
    int? FirstRelevantRank,
    int RetrievedRelevantCount,
    int ExpectedRelevantCount);

public sealed record AggregateRetrievalMetrics(
    int QueryCount,
    double HitAt1,
    double HitAt5,
    double PrecisionAt5,
    double RecallAt5,
    double RecallAt10,
    double MeanReciprocalRank,
    double? MeanFirstRelevantRank,
    double MeanRetrievedRelevantCount,
    double MeanExpectedRelevantCount);

public static class RetrievalMetricCalculator
{
    public static RetrievalMetrics Calculate(
        IReadOnlyList<Guid> retrievedInspectionIds,
        IReadOnlySet<Guid> expectedInspectionIds)
    {
        ArgumentNullException.ThrowIfNull(retrievedInspectionIds);
        ArgumentNullException.ThrowIfNull(expectedInspectionIds);

        if (retrievedInspectionIds.Count != retrievedInspectionIds.Distinct().Count())
        {
            throw new InvalidOperationException("A benchmark retriever returned duplicate inspection IDs.");
        }

        if (expectedInspectionIds.Count == 0)
        {
            throw new ArgumentException("At least one expected inspection ID is required.", nameof(expectedInspectionIds));
        }

        var firstRelevantRank = retrievedInspectionIds
            .Select((inspectionId, index) => new { inspectionId, Rank = index + 1 })
            .FirstOrDefault(result => expectedInspectionIds.Contains(result.inspectionId))?.Rank;
        var retrievedRelevantCount = retrievedInspectionIds.Count(expectedInspectionIds.Contains);

        return new RetrievalMetrics(
            HitAt1: HitAt(retrievedInspectionIds, expectedInspectionIds, 1),
            HitAt5: HitAt(retrievedInspectionIds, expectedInspectionIds, 5),
            PrecisionAt5: PrecisionAt(retrievedInspectionIds, expectedInspectionIds, 5),
            RecallAt5: RecallAt(retrievedInspectionIds, expectedInspectionIds, 5),
            RecallAt10: RecallAt(retrievedInspectionIds, expectedInspectionIds, 10),
            ReciprocalRank: firstRelevantRank is null ? 0d : 1d / firstRelevantRank.Value,
            FirstRelevantRank: firstRelevantRank,
            RetrievedRelevantCount: retrievedRelevantCount,
            ExpectedRelevantCount: expectedInspectionIds.Count);
    }

    public static AggregateRetrievalMetrics Aggregate(IEnumerable<RetrievalMetrics> metrics)
    {
        var values = metrics.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one metric result is required.", nameof(metrics));
        }

        var foundRanks = values
            .Where(metric => metric.FirstRelevantRank is not null)
            .Select(metric => (double)metric.FirstRelevantRank!.Value)
            .ToArray();

        return new AggregateRetrievalMetrics(
            values.Length,
            values.Average(metric => metric.HitAt1),
            values.Average(metric => metric.HitAt5),
            values.Average(metric => metric.PrecisionAt5),
            values.Average(metric => metric.RecallAt5),
            values.Average(metric => metric.RecallAt10),
            values.Average(metric => metric.ReciprocalRank),
            foundRanks.Length == 0 ? null : foundRanks.Average(),
            values.Average(metric => metric.RetrievedRelevantCount),
            values.Average(metric => metric.ExpectedRelevantCount));
    }

    private static double HitAt(
        IReadOnlyList<Guid> retrievedInspectionIds,
        IReadOnlySet<Guid> expectedInspectionIds,
        int depth)
    {
        return retrievedInspectionIds
            .Take(depth)
            .Any(expectedInspectionIds.Contains)
            ? 1d
            : 0d;
    }

    private static double PrecisionAt(
        IReadOnlyList<Guid> retrievedInspectionIds,
        IReadOnlySet<Guid> expectedInspectionIds,
        int depth)
    {
        return retrievedInspectionIds
            .Take(depth)
            .Count(expectedInspectionIds.Contains) / (double)depth;
    }

    private static double RecallAt(
        IReadOnlyList<Guid> retrievedInspectionIds,
        IReadOnlySet<Guid> expectedInspectionIds,
        int depth)
    {
        return retrievedInspectionIds
            .Take(depth)
            .Count(expectedInspectionIds.Contains) / (double)expectedInspectionIds.Count;
    }
}
