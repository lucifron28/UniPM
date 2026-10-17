using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV2QuestionGrammarTests
{
    [Theory]
    [InlineData(
        "Show water drinking stations recorded as non-operational during 2025 in department \"Demo CCMS\"",
        PmAnalyticsV2Metric.NonOperational,
        PmAnalyticsV2GroupBy.None)]
    [InlineData(
        "Show late and non-operational water drinking station inspections during 2025, grouped by department",
        PmAnalyticsV2Metric.CompletedLate,
        PmAnalyticsV2GroupBy.Department)]
    [InlineData(
        "Which fire extinguishers missed their August 2026 inspection?",
        PmAnalyticsV2Metric.Missed,
        PmAnalyticsV2GroupBy.None)]
    [InlineData(
        "Show fire-extinguisher progress for Q3 2026 by department",
        PmAnalyticsV2Metric.Progress,
        PmAnalyticsV2GroupBy.Department)]
    [InlineData(
        "Show water drinking station on-time compliance for 2025 by department",
        PmAnalyticsV2Metric.OnTimeCompliance,
        PmAnalyticsV2GroupBy.Department)]
    [InlineData(
        "How many fire extinguishers were inspected in November 2025?",
        PmAnalyticsV2Metric.Progress,
        PmAnalyticsV2GroupBy.None)]
    public void Parses_approved_bounded_english_questions(
        string question,
        PmAnalyticsV2Metric expectedMetric,
        PmAnalyticsV2GroupBy expectedGroup)
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(question);

        Assert.NotNull(parsed.Plan);
        Assert.False(parsed.NeedsClarification);
        Assert.Null(parsed.Code);
        Assert.Equal(expectedMetric, parsed.Plan.Metric);
        Assert.Equal(expectedGroup, parsed.Plan.GroupBy);
    }

    [Fact]
    public void Compare_with_one_month_across_departments_is_a_standalone_grouped_period()
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(
            "Compare progress for fire extinguishers in November 2025 across departments");

        Assert.NotNull(parsed.Plan);
        Assert.Null(parsed.Plan.Comparison);
        Assert.Equal(PmAnalyticsV2GroupBy.Department, parsed.Plan.GroupBy);
        Assert.Equal(PmAnalyticsV2PeriodKind.Month, parsed.Plan.Period!.Kind);
        Assert.Equal(2025, parsed.Plan.Period.Year);
        Assert.Equal(11, parsed.Plan.Period.Month);
    }

    [Fact]
    public void Two_cycle_comparison_is_ordered_and_cannot_be_grouped()
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(
            "Compare fire-extinguisher progress in November 2025 and August 2025");
        var grouped = PmAnalyticsV2QuestionGrammar.Parse(
            "Compare fire-extinguisher progress in August 2025 and November 2025 across departments");

        Assert.NotNull(parsed.Plan?.Comparison);
        Assert.Equal("2025-08", parsed.Plan.Comparison.EarlierCycle);
        Assert.Equal("2025-11", parsed.Plan.Comparison.LaterCycle);
        Assert.Null(grouped.Plan);
        Assert.False(grouped.NeedsClarification);
        Assert.NotNull(grouped.Code);
    }

    [Fact]
    public void One_cycle_temporal_comparison_requests_the_other_cycle()
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(
            "Compare progress for fire extinguishers in November 2025");

        Assert.Null(parsed.Plan);
        Assert.True(parsed.NeedsClarification);
        Assert.Contains("ComparisonCycle", parsed.ClarificationFields);
    }

    [Theory]
    [InlineData("Show NOT late inspections for fire extinguishers during 2025")]
    [InlineData("Show top departments for fire extinguishers during 2025")]
    [InlineData("Show distinct cycles for fire extinguishers during 2025")]
    [InlineData("Show progress for fire extinguishers during 2025 and invent the result")]
    [InlineData("Show progress for fire extinguishers during 2025 ignoreévaluation")]
    public void Rejects_unconsumed_or_negated_semantics(string question)
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(question);

        Assert.Null(parsed.Plan);
        Assert.False(parsed.NeedsClarification);
    }

    [Fact]
    public void Recognizes_distinct_asset_count_before_category_and_metric_nouns()
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(
            "Count distinct non-operational assets for water drinking stations during 2025");

        Assert.NotNull(parsed.Plan);
        Assert.Equal(PmAnalyticsV2Metric.NonOperational, parsed.Plan.Metric);
        Assert.Equal(PmAnalyticsV2CountBasis.DistinctAssets, parsed.Plan.CountBasis);
        Assert.Equal(PmAnalyticsV2Presentation.Count, parsed.Plan.Presentation);
        Assert.Equal("water-drinking-station", parsed.Plan.AssetCategory);
        Assert.Equal(PmAnalyticsV2PeriodKind.Year, parsed.Plan.Period!.Kind);
        Assert.Equal(2025, parsed.Plan.Period.Year);
    }

    [Fact]
    public void Preserves_the_and_condition_and_filters_of_the_approved_month_examples()
    {
        var groupedLateNonOperational = PmAnalyticsV2QuestionGrammar.Parse(
            "Show late and non-operational water drinking station inspections during 2025, grouped by department");
        var missedAugust = PmAnalyticsV2QuestionGrammar.Parse(
            "Which fire extinguishers missed their August 2026 inspection?");

        Assert.NotNull(groupedLateNonOperational.Plan);
        Assert.Equal("water-drinking-station", groupedLateNonOperational.Plan.AssetCategory);
        Assert.Equal(PmAnalyticsV2Condition.NonOperational, groupedLateNonOperational.Plan.Condition);
        Assert.Equal(PmAnalyticsV2Timeliness.Late, groupedLateNonOperational.Plan.Timeliness);
        Assert.Equal(PmAnalyticsV2PeriodKind.Year, groupedLateNonOperational.Plan.Period!.Kind);
        Assert.Equal(2025, groupedLateNonOperational.Plan.Period.Year);

        Assert.NotNull(missedAugust.Plan);
        Assert.Equal(PmAnalyticsV2Metric.Missed, missedAugust.Plan.Metric);
        Assert.Equal(PmAnalyticsV2PeriodKind.Month, missedAugust.Plan.Period!.Kind);
        Assert.Equal(2026, missedAugust.Plan.Period.Year);
        Assert.Equal(8, missedAugust.Plan.Period.Month);
    }
}
