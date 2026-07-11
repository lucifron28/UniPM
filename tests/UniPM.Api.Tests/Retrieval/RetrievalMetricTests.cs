using UniPM.RetrievalBenchmark;

namespace UniPM.Api.Tests.Retrieval;

public sealed class RetrievalMetricTests
{
    private static readonly Guid RelevantOne = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RelevantTwo = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Distractor = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid DistractorTwo = Guid.Parse("00000000-0000-0000-0000-000000000098");
    private static readonly Guid DistractorThree = Guid.Parse("00000000-0000-0000-0000-000000000097");
    private static readonly Guid DistractorFour = Guid.Parse("00000000-0000-0000-0000-000000000096");

    [Fact]
    public void Metrics_cover_no_result_first_result_rank_five_and_multiple_relevant_records()
    {
        var none = RetrievalMetricCalculator.Calculate([Distractor], new HashSet<Guid> { RelevantOne });
        var first = RetrievalMetricCalculator.Calculate([RelevantOne, Distractor], new HashSet<Guid> { RelevantOne });
        var rankFive = RetrievalMetricCalculator.Calculate(
            [Distractor, DistractorTwo, DistractorThree, DistractorFour, RelevantOne],
            new HashSet<Guid> { RelevantOne });
        var multiple = RetrievalMetricCalculator.Calculate(
            [RelevantOne, Distractor, RelevantTwo],
            new HashSet<Guid> { RelevantOne, RelevantTwo });

        Assert.Equal(0d, none.HitAt1);
        Assert.Equal(0d, none.ReciprocalRank);
        Assert.Equal(1, first.FirstRelevantRank);
        Assert.Equal(1d, first.HitAt1);
        Assert.Equal(5, rankFive.FirstRelevantRank);
        Assert.Equal(0.2d, rankFive.ReciprocalRank);
        Assert.Equal(2, multiple.RetrievedRelevantCount);
        Assert.Equal(1d, multiple.RecallAt5);
    }

    [Fact]
    public void Metrics_use_fixed_precision_depth_and_recall_expected_count()
    {
        var metrics = RetrievalMetricCalculator.Calculate(
            [RelevantOne, Distractor],
            new HashSet<Guid> { RelevantOne, RelevantTwo });

        Assert.Equal(0.2d, metrics.PrecisionAt5);
        Assert.Equal(0.5d, metrics.RecallAt5);
        Assert.Equal(0.5d, metrics.RecallAt10);
    }

    [Fact]
    public void Duplicate_result_ids_are_an_execution_error()
    {
        Assert.Throws<InvalidOperationException>(() => RetrievalMetricCalculator.Calculate(
            [RelevantOne, RelevantOne],
            new HashSet<Guid> { RelevantOne }));
    }

    [Fact]
    public void Aggregate_metrics_are_macro_averaged_deterministically()
    {
        var first = RetrievalMetricCalculator.Calculate([RelevantOne], new HashSet<Guid> { RelevantOne });
        var none = RetrievalMetricCalculator.Calculate([Distractor], new HashSet<Guid> { RelevantOne });

        var aggregate = RetrievalMetricCalculator.Aggregate([first, none]);

        Assert.Equal(2, aggregate.QueryCount);
        Assert.Equal(0.5d, aggregate.HitAt1);
        Assert.Equal(0.5d, aggregate.MeanReciprocalRank);
        Assert.Equal(1d, aggregate.MeanExpectedRelevantCount);
    }
}
