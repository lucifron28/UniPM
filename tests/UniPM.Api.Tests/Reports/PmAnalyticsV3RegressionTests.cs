using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3RegressionTests
{
    [Fact]
    public void Scheduled_count_uses_population_even_when_matching_rows_are_filtered()
    {
        var eligible = Enumerable.Range(0, 10).Select(CreateSource).ToArray();
        var matching = eligible.Take(2).ToArray();

        var result = PmAnalyticsV3QueryService.CalculateMeasure(
            PmAnalyticsV3Catalog.ScheduledCount,
            eligible,
            matching,
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ["2025-02"]);

        Assert.Equal(10, result.Numerator);
        Assert.Equal(10, result.Denominator);
        Assert.Equal(10, result.Value);
        Assert.Equal(2, result.MatchingRowCount);
    }

    [Fact]
    public void Compliance_stays_unmeasurable_while_a_selected_cycle_is_open_without_a_schedule_row()
    {
        var eligible = new[] { CreateSource(0) with
        {
            PmCycle = "2024-02",
            Deadline = new DateTimeOffset(2024, 2, 29, 23, 59, 59, TimeSpan.FromHours(8)),
            CompletedAt = new DateTimeOffset(2024, 2, 20, 12, 0, 0, TimeSpan.FromHours(8))
        } };

        var result = PmAnalyticsV3QueryService.CalculateMeasure(
            PmAnalyticsV3Catalog.OnTimeCompliancePercent,
            eligible,
            eligible,
            new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.FromHours(8)),
            ["2024-02", "2025-05"]);

        Assert.Null(result.Value);
        Assert.False(result.IsMeasurable);
        Assert.Contains("active or future", result.UnmeasurableReason);
        Assert.Equal(1, result.Denominator);
    }

    [Fact]
    public void Negated_outcomes_are_clarified_and_not_operational_maps_to_non_operational()
    {
        var currentTime = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var rejected = PmAnalyticsV3RuleParser.Parse(
            "Show completed fire-extinguisher records that were not completed during 2025",
            currentTime);
        var records = PmAnalyticsV3RuleParser.Parse(
            "Show water drinking station records that are not operational during 2025",
            currentTime);

        Assert.Equal("NeedsClarification", rejected.Status);
        Assert.Contains("Measures", rejected.ClarificationFields);
        Assert.Equal("Valid", records.Status);
        Assert.NotNull(records.Plan);
        Assert.Contains(records.Plan.MatchingFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Condition
                && filter.Values?.Contains("NonOperational", StringComparer.Ordinal) == true);
    }

    [Fact]
    public void Preview_token_is_valid_across_instances_and_rejects_tampering_wrong_user_and_expiry()
    {
        var now = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var jwtConfiguration = JwtRuntimeConfiguration.Create(
            new JwtOptions
            {
                Issuer = "UniPM.Tests",
                Audience = "UniPM.Tests.Clients",
                SigningKey = new string('k', JwtOptions.MinimumSigningKeyBytes)
            },
            isDevelopment: false);
        var parsed = PmAnalyticsV3RuleParser.Parse(
            "Show scheduled fire-extinguisher cycles during 2024",
            now);
        Assert.NotNull(parsed.Plan);
        Assert.True(PmAnalyticsV3PlanValidator.TryNormalize(parsed.Plan, out var normalized, out _));

        var firstInstance = new PmAnalyticsV3PreviewStore(clock, jwtConfiguration);
        var secondInstance = new PmAnalyticsV3PreviewStore(clock, jwtConfiguration);
        Assert.True(firstInstance.TryCreate("user-1", normalized, out var token, out _));
        Assert.Equal(PmAnalyticsV3PreviewLookup.Found, secondInstance.TryGet("user-1", token, out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(PmAnalyticsV3PreviewLookup.Unavailable, secondInstance.TryGet("user-2", token, out _));

        var tampered = token[..^1] + (token[^1] == 'a' ? 'b' : 'a');
        Assert.Equal(PmAnalyticsV3PreviewLookup.Unavailable, secondInstance.TryGet("user-1", tampered, out _));

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(PmAnalyticsV3PreviewLookup.Unavailable, secondInstance.TryGet("user-1", token, out _));
    }

    private static PmAnalyticsV3QueryService.PmAnalyticsV3Source CreateSource(int index)
    {
        var deadline = new DateTimeOffset(2024, 2, 29, 23, 59, 59, TimeSpan.FromHours(8));
        return new PmAnalyticsV3QueryService.PmAnalyticsV3Source(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            $"FE-{index}",
            "fire-extinguisher",
            "TEST DEPARTMENT",
            "TEST BUILDING",
            null,
            "2024-02",
            deadline,
            null,
            "Due",
            null,
            null,
            false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        private DateTimeOffset _currentTime = currentTime;

        public override DateTimeOffset GetUtcNow() => _currentTime;

        internal void Advance(TimeSpan duration) => _currentTime += duration;
    }
}
