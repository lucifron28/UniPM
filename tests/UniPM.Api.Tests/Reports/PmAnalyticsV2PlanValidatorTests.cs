using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV2PlanValidatorTests
{
    [Theory]
    [InlineData(PmAnalyticsV2PeriodKind.Month)]
    [InlineData(PmAnalyticsV2PeriodKind.Quarter)]
    public void Rejects_month_and_quarter_periods_without_their_required_value(PmAnalyticsV2PeriodKind kind)
    {
        var period = new PmAnalyticsV2Period(kind, Year: 2025);

        Assert.False(PmAnalyticsV2PlanValidator.TryNormalize(Plan(period), out _, out var errors));
        Assert.Contains("Period", errors.Keys);
    }

    [Fact]
    public void Month_range_expands_only_cpmp_cycles_and_stays_within_twelve_calendar_months()
    {
        var fullYear = Plan(new PmAnalyticsV2Period(
            PmAnalyticsV2PeriodKind.Range,
            StartCycle: "2024-01",
            EndCycle: "2024-12"));
        var thirteenMonths = Plan(new PmAnalyticsV2Period(
            PmAnalyticsV2PeriodKind.Range,
            StartCycle: "2024-01",
            EndCycle: "2025-01"));

        Assert.True(PmAnalyticsV2PlanValidator.TryNormalize(fullYear, out var normalized, out _));
        Assert.Equal(new[] { "2024-02", "2024-05", "2024-08", "2024-11" }, normalized.Cycles);
        Assert.False(PmAnalyticsV2PlanValidator.TryNormalize(thirteenMonths, out _, out var errors));
        Assert.Contains("Period", errors.Keys);
    }

    [Fact]
    public void Single_month_must_match_the_asset_category_schedule_frequency()
    {
        var fireAlarmFebruary = Plan(
            new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Month, Year: 2025, Month: 2),
            assetCategory: "fire-alarm");
        var fireAlarmDecember = Plan(
            new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Month, Year: 2025, Month: 12),
            assetCategory: "fire-alarm");

        Assert.False(PmAnalyticsV2PlanValidator.TryNormalize(fireAlarmFebruary, out _, out _));
        Assert.True(PmAnalyticsV2PlanValidator.TryNormalize(fireAlarmDecember, out var normalized, out _));
        Assert.Equal(new[] { "2025-12" }, normalized.Cycles);
    }

    [Fact]
    public void Rejects_percent_distinct_assets_and_contradictory_outcome_filters()
    {
        var distinctPercent = Plan(
            new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
            presentation: PmAnalyticsV2Presentation.Percent,
            countBasis: PmAnalyticsV2CountBasis.DistinctAssets);
        var futureOperational = Plan(
            new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: 2025),
            condition: PmAnalyticsV2Condition.NonOperational,
            timeliness: PmAnalyticsV2Timeliness.Future);

        Assert.False(PmAnalyticsV2PlanValidator.TryNormalize(distinctPercent, out _, out var distinctErrors));
        Assert.Contains("CountBasis", distinctErrors.Keys);
        Assert.False(PmAnalyticsV2PlanValidator.TryNormalize(futureOperational, out _, out var filterErrors));
        Assert.Contains("Condition", filterErrors.Keys);
    }

    private static PmAnalyticsV2Plan Plan(
        PmAnalyticsV2Period period,
        string assetCategory = "fire-extinguisher",
        PmAnalyticsV2Presentation presentation = PmAnalyticsV2Presentation.Count,
        PmAnalyticsV2CountBasis countBasis = PmAnalyticsV2CountBasis.AssetCycles,
        PmAnalyticsV2Condition? condition = null,
        PmAnalyticsV2Timeliness? timeliness = null) =>
        new(
            2,
            PmAnalyticsV2Metric.Progress,
            assetCategory,
            period,
            null,
            presentation,
            countBasis,
            null,
            null,
            null,
            condition,
            timeliness,
            PmAnalyticsV2GroupBy.None,
            null);
}
