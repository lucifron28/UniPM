using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

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
        var negatedLate = PmAnalyticsV3RuleParser.Parse(
            "Show fire-extinguisher records that were not late in 2025",
            currentTime);
        var zeroLateIntent = PmAnalyticsV3RuleParser.Parse(
            "Show departments with no late fire-extinguisher inspections in 2025",
            currentTime);
        var negatedCategory = PmAnalyticsV3RuleParser.Parse(
            "Show completed records for no fire-alarm systems in 2025",
            currentTime);
        var records = PmAnalyticsV3RuleParser.Parse(
            "Show water drinking station records that are not operational during 2025",
            currentTime);

        Assert.Equal("NeedsClarification", rejected.Status);
        Assert.Contains("Measures", rejected.ClarificationFields);
        Assert.Equal("NeedsClarification", negatedLate.Status);
        Assert.Contains("Measures", negatedLate.ClarificationFields);
        Assert.Equal("NeedsClarification", zeroLateIntent.Status);
        Assert.Equal("NeedsClarification", negatedCategory.Status);
        Assert.Equal("Valid", records.Status);
        Assert.NotNull(records.Plan);
        Assert.Contains(records.Plan.MatchingFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Condition
                && filter.Values?.Contains("NonOperational", StringComparer.Ordinal) == true);
    }

    [Fact]
    public void Ambiguous_dates_filters_and_rank_measures_are_clarified_without_broadening_the_plan()
    {
        var now = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var multipleMonths = PmAnalyticsV3RuleParser.Parse(
            "Show completed fire extinguishers in February and November 2025",
            now);
        var disjunction = PmAnalyticsV3RuleParser.Parse(
            "Show late or non-operational fire extinguisher records in 2025",
            now);
        var ambiguousRanking = PmAnalyticsV3RuleParser.Parse(
            "Show scheduled and completed fire extinguisher counts for the top 5 departments in 2024",
            now);
        var explicitRanking = PmAnalyticsV3RuleParser.Parse(
            "Show scheduled and completed fire extinguisher counts for the top 5 departments by completed count in 2024",
            now);
        var missingRankingScope = PmAnalyticsV3RuleParser.Parse(
            "Show compliance for the most fire alarm systems in 2024",
            now);
        var textualPercentageThreshold = PmAnalyticsV3RuleParser.Parse(
            "Show fire extinguisher compliance at least 80 percent during 2024",
            now);
        var unquotedDepartment = PmAnalyticsV3RuleParser.Parse(
            "Show fire extinguisher compliance for the Research department in 2024",
            now);
        var quotedBuilding = PmAnalyticsV3RuleParser.Parse(
            "Show fire extinguisher compliance in building 'North Block' in 2024",
            now);
        var unsupportedSort = PmAnalyticsV3RuleParser.Parse(
            "Show completed fire extinguisher counts in 2024 sorted by department",
            now);
        var completedRecords = PmAnalyticsV3RuleParser.Parse(
            "Show completed fire extinguisher records in PM cycle year 2024",
            now);

        Assert.Equal("NeedsClarification", multipleMonths.Status);
        Assert.Contains("Period", multipleMonths.ClarificationFields);
        Assert.Equal("NeedsClarification", disjunction.Status);
        Assert.Contains("Filters", disjunction.ClarificationFields);
        Assert.Equal("NeedsClarification", ambiguousRanking.Status);
        Assert.Contains("Top", ambiguousRanking.ClarificationFields);
        Assert.Equal("Valid", explicitRanking.Status);
        Assert.NotNull(explicitRanking.Plan?.Top);
        Assert.Equal(PmAnalyticsV3Catalog.CompletedCount, explicitRanking.Plan.Top.Measure);
        Assert.Equal(new[] { PmAnalyticsV3Catalog.Department }, explicitRanking.Plan.GroupBy);
        Assert.Equal("NeedsClarification", missingRankingScope.Status);
        Assert.Contains("Top", missingRankingScope.ClarificationFields);
        Assert.Contains("GroupBy", missingRankingScope.ClarificationFields);
        Assert.Equal("Valid", textualPercentageThreshold.Status);
        Assert.Single(textualPercentageThreshold.Plan!.AggregateFilters);
        Assert.Equal(80m, textualPercentageThreshold.Plan.AggregateFilters[0].Value);
        Assert.Null(textualPercentageThreshold.Plan.Top);
        Assert.Contains("Department", unquotedDepartment.ClarificationFields);
        Assert.Equal("Valid", quotedBuilding.Status);
        Assert.Contains(quotedBuilding.Plan!.BaseFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Building
                && filter.Values?.Contains("North Block", StringComparer.Ordinal) == true);
        Assert.Equal("Unsupported", unsupportedSort.Status);
        Assert.Equal("Valid", completedRecords.Status);
        Assert.Contains(completedRecords.Plan!.MatchingFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Condition
                && filter.Operator == PmAnalyticsV3FilterOperator.In
                && filter.Values is { } values
                && values.Contains("Operational", StringComparer.Ordinal)
                && values.Contains("NonOperational", StringComparer.Ordinal));
    }

    [Fact]
    public async Task Full_execution_keeps_multicategory_compliance_unmeasurable_for_cycles_missing_in_a_department()
    {
        var department = "SYNTHETIC GSD";
        var plan = CreateAggregatePlan(
            ["fire-extinguisher", "fire-alarm"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Range, StartCycle: "2025-05", EndCycle: "2025-06"),
            [PmAnalyticsV3Catalog.OnTimeCompliancePercent],
            [PmAnalyticsV3Catalog.Department],
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.OnTimeCompliancePercent]);

        var response = await ExecutePlanAsync(
            plan,
            context => AddSchedule(context, "FE-TEST-001", "fire-extinguisher", department, "2025-05"),
            new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.FromHours(8)));

        var group = Assert.Single(response.AggregateRows);
        var compliance = group.Measures[PmAnalyticsV3Catalog.OnTimeCompliancePercent];
        Assert.Equal(department, group.Values[PmAnalyticsV3Catalog.Department]);
        Assert.Null(compliance.Value);
        Assert.False(compliance.IsMeasurable);
        Assert.Equal(1, compliance.Denominator);
        Assert.Contains("active or future", compliance.UnmeasurableReason);
    }

    [Fact]
    public async Task Full_execution_ranks_bottom_groups_then_honors_requested_output_order()
    {
        var plan = CreateAggregatePlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2024),
            [PmAnalyticsV3Catalog.ScheduledCount],
            [PmAnalyticsV3Catalog.Department],
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.ScheduledCount],
            [new PmAnalyticsV3OrderBy(PmAnalyticsV3Catalog.Department, PmAnalyticsV3SortDirection.Descending)],
            new PmAnalyticsV3Top(PmAnalyticsV3Catalog.ScheduledCount, 2, PmAnalyticsV3SortDirection.Ascending));

        var response = await ExecutePlanAsync(
            plan,
            context =>
            {
                AddSchedules(context, "A DEPARTMENT", 3);
                AddSchedules(context, "B DEPARTMENT", 5);
                AddSchedules(context, "C DEPARTMENT", 4);
            },
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(3, response.Population.GroupsBeforeAggregateFilters);
        Assert.Equal(2, response.Population.GroupsReturnedAfterTop);
        Assert.Collection(
            response.AggregateRows,
            row => Assert.Equal("C DEPARTMENT", row.Values[PmAnalyticsV3Catalog.Department]),
            row => Assert.Equal("A DEPARTMENT", row.Values[PmAnalyticsV3Catalog.Department]));
    }

    [Fact]
    public async Task Catalog_suggestions_deduplicate_before_applying_the_result_limit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-pm-analytics-v3-catalog-{Guid.NewGuid():N}")
            .Options;
        await using (var context = new ApplicationDbContext(options))
        {
            context.Assets.AddRange(
                Enumerable.Range(0, 210).Select(index => CreateAsset($"FE-{index:000}", "Research"))
                    .Append(CreateAsset("FE-ZEBRA", "Research Operations")));
            await context.SaveChangesAsync();
        }

        var service = new PmAnalyticsV3QueryService(new TestContextFactory(options), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var response = await service.FindFilterValuesAsync(
            PmAnalyticsV3Catalog.Department,
            ["fire-extinguisher"],
            "Research",
            CancellationToken.None);

        Assert.Equal(["Research", "Research Operations"], response.Values);
        Assert.False(response.HasMore);
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

    private static PmAnalyticsV3ReportPlan CreateAggregatePlan(
        IReadOnlyList<string> categories,
        PmAnalyticsV3Period period,
        IReadOnlyList<string> measures,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<string> columns,
        IReadOnlyList<PmAnalyticsV3OrderBy>? orderBy = null,
        PmAnalyticsV3Top? top = null) => new(
            3,
            PmAnalyticsV3Catalog.Version,
            PmAnalyticsV3Catalog.SubjectPmExecution,
            PmAnalyticsV3Mode.Aggregate,
            period,
            categories,
            measures,
            groupBy,
            [],
            [],
            [],
            orderBy ?? [],
            top,
            PmAnalyticsV3Presentation.Table,
            columns);

    private static async Task<PmAnalyticsV3ExecutionResponse> ExecutePlanAsync(
        PmAnalyticsV3ReportPlan plan,
        Action<ApplicationDbContext> seed,
        DateTimeOffset now)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-pm-analytics-v3-execution-{Guid.NewGuid():N}")
            .Options;
        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            seed(context);
            await context.SaveChangesAsync();
        }

        var service = new PmAnalyticsV3QueryService(new TestContextFactory(options), new FixedTimeProvider(now));
        var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
        Assert.NotNull(resolution.Plan);
        return await service.ExecuteAsync(resolution.Plan, 1, 100, CancellationToken.None);
    }

    private static Asset CreateAsset(
        string assetCode,
        string department,
        string category = "fire-extinguisher") => new()
    {
        Id = Guid.NewGuid(),
        AssetCode = assetCode,
        AssetCategory = category,
        Department = department,
        Building = "Synthetic Building",
        Location = "Synthetic Location",
        Status = "Active"
    };

    private static void AddSchedules(ApplicationDbContext context, string department, int count)
    {
        for (var index = 0; index < count; index++)
        {
            AddSchedule(context, $"{department.Replace(' ', '-')}-{index:000}", "fire-extinguisher", department, "2024-02");
        }
    }

    private static void AddSchedule(
        ApplicationDbContext context,
        string assetCode,
        string category,
        string department,
        string cycle)
    {
        var asset = CreateAsset(assetCode, department, category);
        var year = int.Parse(cycle[..4], System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(cycle[5..], System.Globalization.CultureInfo.InvariantCulture);
        context.Assets.Add(asset);
        context.PreventiveMaintenanceSchedules.Add(new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            PmCycle = cycle,
            ScheduleDate = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Due
        });
    }

    private sealed class TestContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(options));
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        private DateTimeOffset _currentTime = currentTime;

        public override DateTimeOffset GetUtcNow() => _currentTime;

        internal void Advance(TimeSpan duration) => _currentTime += duration;
    }
}
