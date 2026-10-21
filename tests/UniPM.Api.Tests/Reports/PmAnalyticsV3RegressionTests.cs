using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;
using System.Text.Json;

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
    public void Comparing_multiple_categories_groups_results_by_category()
    {
        var result = PmAnalyticsV3RuleParser.Parse(
            "Compare scheduled fire extinguisher and fire alarm cycles in 2024",
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("Valid", result.Status);
        Assert.NotNull(result.Plan);
        Assert.Equal(
            new[] { "fire-extinguisher", "fire-alarm" },
            result.Plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.AssetCategory], result.Plan.GroupBy);
        Assert.Contains(PmAnalyticsV3Catalog.AssetCategory, result.Plan.Columns);
    }

    [Fact]
    public void Clearly_out_of_scope_asset_categories_are_rejected_without_a_plan()
    {
        var result = PmAnalyticsV3RuleParser.Parse(
            "Show elevator maintenance compliance in 2024",
            new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("Unsupported", result.Status);
        Assert.Equal("UnsupportedAssetCategory", result.Code);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Question_parser_selects_and_validates_pm_and_inventory_subjects_without_crossing_scope()
    {
        var now = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var pm = PmAnalyticsV3RuleParser.Parse(
            "Show progress for fire extinguishers during 2024",
            now);
        var inspectionCount = PmAnalyticsV3RuleParser.Parse(
            "How many emergency lights were inspected in the June 2026 PM cycle?",
            now);
        var inventory = PmAnalyticsV3RuleParser.Parse(
            "How many fire extinguishers are registered in the CCMS building by status?",
            now);
        var ambiguous = PmAnalyticsV3RuleParser.Parse(
            "How many emergency lights are in the CCMS building?",
            now);
        var crossSubject = PmAnalyticsV3RuleParser.Parse(
            "How many fire extinguishers are registered in the CCMS building with overdue inspections?",
            now);
        var historicalInventory = PmAnalyticsV3RuleParser.Parse(
            "How many fire extinguishers are registered in 2025?",
            now);

        Assert.Equal("Valid", pm.Status);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, pm.Plan?.Subject);
        Assert.True(PmAnalyticsV3PlanValidator.TryNormalize(pm.Plan!, out _, out _));
        Assert.Equal("Valid", inspectionCount.Status);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, inspectionCount.Plan?.Subject);
        Assert.Equal(new[] { PmAnalyticsV3Catalog.CompletedCount }, inspectionCount.Plan?.Measures);
        Assert.True(PmAnalyticsV3PlanValidator.TryNormalize(inspectionCount.Plan!, out _, out _));

        Assert.Equal("Valid", inventory.Status);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectAssetInventory, inventory.Plan?.Subject);
        Assert.Null(inventory.Plan?.Period);
        Assert.Equal(new[] { PmAnalyticsV3Catalog.RegisteredAssetCount }, inventory.Plan?.Measures);
        Assert.Equal(new[] { PmAnalyticsV3Catalog.RegistrationStatus }, inventory.Plan?.GroupBy);
        Assert.Contains(inventory.Plan!.BaseFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Building
                && filter.Values?.Contains("CCMS", StringComparer.Ordinal) == true);
        Assert.True(PmAnalyticsV3PlanValidator.TryNormalize(inventory.Plan, out _, out _));

        Assert.Equal("NeedsClarification", ambiguous.Status);
        Assert.Contains("Subject", ambiguous.ClarificationFields);
        Assert.Equal("Unsupported", crossSubject.Status);
        Assert.Equal("CrossSubjectQuestionNotSupported", crossSubject.Code);
        Assert.Null(crossSubject.Plan);
        Assert.Equal("Unsupported", historicalInventory.Status);
        Assert.Equal("HistoricalInventoryNotSupported", historicalInventory.Code);

        var planSchema = PmAnalyticsV3ModelPrompt.OutputSchema
            .GetProperty("properties")
            .GetProperty("plan")
            .GetProperty("anyOf")[0];
        var subjects = planSchema
            .GetProperty("properties")
            .GetProperty("subject")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains(PmAnalyticsV3Catalog.SubjectPmExecution, subjects);
        Assert.Contains(PmAnalyticsV3Catalog.SubjectAssetInventory, subjects);
        Assert.Contains(
            planSchema.GetProperty("properties").GetProperty("period").GetProperty("anyOf")
                .EnumerateArray(),
            item => item.GetProperty("type").GetString() == "null");
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
    public async Task Synthetic_denominator_fixture_keeps_matching_filters_out_of_the_denominator()
    {
        using var fixture = LoadCalculationFixture("denominator-matching");
        var plan = CreateAggregatePlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Month, Year: 2024, Month: 2),
            [PmAnalyticsV3Catalog.CompletedCount],
            [],
            [PmAnalyticsV3Catalog.CompletedCount]) with
        {
            MatchingFilters = [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])]
        };

        var response = await ExecuteFixtureAsync(fixture.RootElement, plan);
        var result = Assert.Single(response.AggregateRows).Measures[PmAnalyticsV3Catalog.CompletedCount];
        var expected = fixture.RootElement.GetProperty("expected");

        Assert.Equal(expected.GetProperty("eligibleCount").GetInt32(), response.Population.EligibleAssetCycleCount);
        Assert.Equal(expected.GetProperty("matchingCount").GetInt32(), result.MatchingRowCount);
        Assert.Equal(expected.GetProperty("completedCountNumerator").GetInt32(), result.Numerator);
        Assert.Equal(expected.GetProperty("denominator").GetInt32(), result.Denominator);
        Assert.Equal(expected.GetProperty("completedCountValue").GetDecimal(), result.Value);
    }

    [Fact]
    public async Task Synthetic_grouping_fixture_applies_thresholds_to_group_level_denominators()
    {
        using var fixture = LoadCalculationFixture("grouped-threshold");
        var plan = CreateAggregatePlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Month, Year: 2024, Month: 2),
            [PmAnalyticsV3Catalog.OnTimeCompliancePercent],
            [PmAnalyticsV3Catalog.Department],
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.OnTimeCompliancePercent],
            aggregateFilters: [new PmAnalyticsV3AggregateFilter(
                PmAnalyticsV3Catalog.OnTimeCompliancePercent,
                PmAnalyticsV3NumericOperator.GreaterThanOrEqual,
                60m)]);

        var response = await ExecuteFixtureAsync(fixture.RootElement, plan);
        var expected = fixture.RootElement.GetProperty("expected");
        var row = Assert.Single(response.AggregateRows);
        var result = row.Measures[PmAnalyticsV3Catalog.OnTimeCompliancePercent];

        Assert.Equal(expected.GetProperty("eligibleCount").GetInt32(), response.Population.EligibleAssetCycleCount);
        Assert.Equal(expected.GetProperty("groupCountBeforeThreshold").GetInt32(), response.Population.GroupsBeforeAggregateFilters);
        Assert.Equal(expected.GetProperty("groupCountAfterThreshold").GetInt32(), response.Population.GroupsAfterAggregateFilters);
        Assert.Equal(expected.GetProperty("keptDepartment").GetString(), row.Values[PmAnalyticsV3Catalog.Department]);
        Assert.Equal(expected.GetProperty("keptNumerator").GetInt32(), result.Numerator);
        Assert.Equal(expected.GetProperty("keptDenominator").GetInt32(), result.Denominator);
        Assert.Equal(expected.GetProperty("keptPercent").GetDecimal(), result.Value);
    }

    [Fact]
    public async Task Synthetic_category_fixture_uses_category_specific_cycle_calendars()
    {
        using var fixture = LoadCalculationFixture("category-cycles");
        var plan = CreateAggregatePlan(
            ["fire-extinguisher", "fire-alarm"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2024),
            [PmAnalyticsV3Catalog.ScheduledCount],
            [PmAnalyticsV3Catalog.AssetCategory],
            [PmAnalyticsV3Catalog.AssetCategory, PmAnalyticsV3Catalog.ScheduledCount]);

        var response = await ExecuteFixtureAsync(fixture.RootElement, plan);
        var expected = fixture.RootElement.GetProperty("expected");

        Assert.Equal(
            expected.GetProperty("fire-extinguisher").EnumerateArray().Select(item => item.GetString()),
            response.CyclesByCategory["fire-extinguisher"]);
        Assert.Equal(
            expected.GetProperty("fire-alarm").EnumerateArray().Select(item => item.GetString()),
            response.CyclesByCategory["fire-alarm"]);
        Assert.Equal(expected.GetProperty("eligibleCount").GetInt32(), response.Population.EligibleAssetCycleCount);
        Assert.Equal(expected.GetProperty("scheduledByCategory").GetProperty("fire-extinguisher").GetInt32(),
            Convert.ToInt32(response.AggregateRows.Single(row => Equals(row.Values[PmAnalyticsV3Catalog.AssetCategory], "fire-extinguisher")).Measures[PmAnalyticsV3Catalog.ScheduledCount].Value));
        Assert.Equal(expected.GetProperty("scheduledByCategory").GetProperty("fire-alarm").GetInt32(),
            Convert.ToInt32(response.AggregateRows.Single(row => Equals(row.Values[PmAnalyticsV3Catalog.AssetCategory], "fire-alarm")).Measures[PmAnalyticsV3Catalog.ScheduledCount].Value));
    }

    [Fact]
    public async Task Synthetic_quarter_source_fixture_preserves_categories_period_filters_and_sources()
    {
        using var fixture = LoadCalculationFixture("quarter-source-records");
        var expected = fixture.RootElement.GetProperty("expected");
        var firstQuarter = await ExecuteFixtureAsync(fixture.RootElement, CreateRecordsPlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Quarter, Year: 2025, Quarter: 1),
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Building, PmAnalyticsV3FilterOperator.Equal, ["NORTH"])],
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])]));
        var fourthQuarter = await ExecuteFixtureAsync(fixture.RootElement, CreateRecordsPlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Quarter, Year: 2025, Quarter: 4),
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Building, PmAnalyticsV3FilterOperator.Equal, ["NORTH"])],
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])]));

        Assert.Equal(["fire-extinguisher"], firstQuarter.Plan.Categories);
        Assert.Equal(expected.GetProperty("quarter1Cycles").EnumerateArray().Select(item => item.GetString()),
            firstQuarter.CyclesByCategory["fire-extinguisher"]);
        Assert.Equal(expected.GetProperty("quarter1RecordCodes").EnumerateArray().Select(item => item.GetString()),
            firstQuarter.Records.Select(record => record.Values["assetCode"]));
        Assert.Equal(["fire-extinguisher"], fourthQuarter.Plan.Categories);
        Assert.Equal(expected.GetProperty("quarter4Cycles").EnumerateArray().Select(item => item.GetString()),
            fourthQuarter.CyclesByCategory["fire-extinguisher"]);
        Assert.Equal(expected.GetProperty("quarter4RecordCodes").EnumerateArray().Select(item => item.GetString()),
            fourthQuarter.Records.Select(record => record.Values["assetCode"]));
    }

    [Fact]
    public async Task Synthetic_pagination_fixture_returns_stable_nonduplicated_source_records()
    {
        using var fixture = LoadCalculationFixture("records-pagination");
        var expected = fixture.RootElement.GetProperty("expected");
        var plan = CreateRecordsPlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Month, Year: 2024, Month: 2),
            [],
            [new PmAnalyticsV3Filter(PmAnalyticsV3Catalog.Condition, PmAnalyticsV3FilterOperator.Equal, ["NonOperational"])]);

        var response = await ExecuteFixtureAsync(
            fixture.RootElement,
            plan,
            expected.GetProperty("page").GetInt32(),
            expected.GetProperty("pageSize").GetInt32());

        Assert.Equal(expected.GetProperty("eligibleCount").GetInt32(), response.Population.EligibleAssetCycleCount);
        Assert.Equal(expected.GetProperty("matchingCount").GetInt32(), response.Population.MatchingAssetCycleCount);
        Assert.Equal(expected.GetProperty("totalPages").GetInt32(), response.TotalPages);
        Assert.Equal(expected.GetProperty("recordCodes").EnumerateArray().Select(item => item.GetString()),
            response.Records.Select(record => record.Values["assetCode"]));
        Assert.Equal(response.Records.Count, response.Records.Select(record => record.ScheduleId).Distinct().Count());
    }

    [Fact]
    public async Task Synthetic_missing_coverage_and_empty_fixtures_are_unmeasurable_without_invented_values()
    {
        using var missingFixture = LoadCalculationFixture("missing-category-coverage");
        var missingPlan = CreateAggregatePlan(
            ["fire-extinguisher", "fire-alarm"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Range, StartCycle: "2025-05", EndCycle: "2025-06"),
            [PmAnalyticsV3Catalog.OnTimeCompliancePercent],
            [PmAnalyticsV3Catalog.Department],
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.OnTimeCompliancePercent]);
        var missing = await ExecuteFixtureAsync(missingFixture.RootElement, missingPlan);
        var missingExpected = missingFixture.RootElement.GetProperty("expected");
        var missingMeasure = Assert.Single(missing.AggregateRows).Measures[PmAnalyticsV3Catalog.OnTimeCompliancePercent];

        Assert.Equal(missingExpected.GetProperty("eligibleCount").GetInt32(), missingMeasure.Denominator);
        Assert.False(missingMeasure.IsMeasurable);
        Assert.Null(missingMeasure.Value);
        Assert.Contains(missingExpected.GetProperty("reasonContains").GetString()!, missingMeasure.UnmeasurableReason);
        Assert.Equal(missingExpected.GetProperty("fire-alarm").EnumerateArray().Select(item => item.GetString()), missing.CyclesByCategory["fire-alarm"]);

        using var emptyFixture = LoadCalculationFixture("zero-eligible");
        var emptyPlan = CreateAggregatePlan(
            ["fire-extinguisher"],
            new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, Year: 2024),
            [PmAnalyticsV3Catalog.ProgressPercent],
            [],
            [PmAnalyticsV3Catalog.ProgressPercent]);
        var empty = await ExecuteFixtureAsync(emptyFixture.RootElement, emptyPlan);
        var emptyExpected = emptyFixture.RootElement.GetProperty("expected");
        var emptyMeasure = Assert.Single(empty.AggregateRows).Measures[PmAnalyticsV3Catalog.ProgressPercent];

        Assert.Equal(emptyExpected.GetProperty("eligibleCount").GetInt32(), empty.Population.EligibleAssetCycleCount);
        Assert.Equal(emptyExpected.GetProperty("denominator").GetInt32(), emptyMeasure.Denominator);
        Assert.False(emptyMeasure.IsMeasurable);
        Assert.Null(emptyMeasure.Value);
        Assert.Contains(emptyExpected.GetProperty("reasonContains").GetString()!, emptyMeasure.UnmeasurableReason);
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
        PmAnalyticsV3Top? top = null,
        IReadOnlyList<PmAnalyticsV3AggregateFilter>? aggregateFilters = null) => new(
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
            aggregateFilters ?? [],
            orderBy ?? [],
            top,
            PmAnalyticsV3Presentation.Table,
            columns);

    private static PmAnalyticsV3ReportPlan CreateRecordsPlan(
        IReadOnlyList<string> categories,
        PmAnalyticsV3Period period,
        IReadOnlyList<PmAnalyticsV3Filter> baseFilters,
        IReadOnlyList<PmAnalyticsV3Filter> matchingFilters) => new(
            3,
            PmAnalyticsV3Catalog.Version,
            PmAnalyticsV3Catalog.SubjectPmExecution,
            PmAnalyticsV3Mode.Records,
            period,
            categories,
            [],
            [],
            baseFilters,
            matchingFilters,
            [],
            [],
            null,
            PmAnalyticsV3Presentation.Table,
            ["assetCode", PmAnalyticsV3Catalog.AssetCategory, PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.Building, PmAnalyticsV3Catalog.PmCycle, PmAnalyticsV3Catalog.Condition]);

    private static JsonDocument LoadCalculationFixture(string fixtureId)
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "reference/evaluation/pm-analytics-v3/synthetic-calculation-fixtures.json")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new DirectoryNotFoundException("Could not locate the synthetic PM Analytics V3 fixtures.");
        }

        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(current.FullName, "reference/evaluation/pm-analytics-v3/synthetic-calculation-fixtures.json")));
        var fixture = document.RootElement.GetProperty("fixtures").EnumerateArray()
            .SingleOrDefault(item => item.GetProperty("fixtureId").GetString() == fixtureId);
        if (fixture.ValueKind == JsonValueKind.Undefined)
        {
            document.Dispose();
            throw new KeyNotFoundException($"Synthetic fixture '{fixtureId}' was not found.");
        }

        return JsonDocument.Parse(fixture.GetRawText());
    }

    private static async Task<PmAnalyticsV3ExecutionResponse> ExecuteFixtureAsync(
        JsonElement fixture,
        PmAnalyticsV3ReportPlan plan,
        int page = 1,
        int pageSize = 100)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"unipm-pm-analytics-v3-fixture-{Guid.NewGuid():N}")
            .Options;
        await using (var context = new ApplicationDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            foreach (var entry in fixture.GetProperty("assets").EnumerateArray())
            {
                var asset = CreateAsset(
                    entry.GetProperty("assetCode").GetString()!,
                    entry.GetProperty("department").GetString()!,
                    entry.GetProperty("category").GetString()!);
                asset.Building = entry.GetProperty("building").GetString();
                asset.Location = "Synthetic Location";
                var cycle = entry.GetProperty("cycle").GetString()!;
                var year = int.Parse(cycle[..4], System.Globalization.CultureInfo.InvariantCulture);
                var month = int.Parse(cycle[5..], System.Globalization.CultureInfo.InvariantCulture);
                var schedule = new PreventiveMaintenanceSchedule
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    Asset = asset,
                    PmCycle = cycle,
                    ScheduleDate = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.FromHours(8)),
                    PeriodType = "Quarter",
                    Status = ScheduleStatusCatalog.Due
                };
                context.Assets.Add(asset);
                context.PreventiveMaintenanceSchedules.Add(schedule);
                if (entry.GetProperty("completedAt").ValueKind == JsonValueKind.String)
                {
                    context.InspectionRecords.Add(new InspectionRecord
                    {
                        Id = Guid.NewGuid(),
                        ScheduleId = schedule.Id,
                        Schedule = schedule,
                        AssetId = asset.Id,
                        Asset = asset,
                        InspectorUserId = Guid.NewGuid(),
                        DateInspected = DateTimeOffset.Parse(entry.GetProperty("completedAt").GetString()!),
                        CompletedAt = DateTimeOffset.Parse(entry.GetProperty("completedAt").GetString()!),
                        IsOperational = entry.GetProperty("isOperational").GetBoolean()
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        var now = DateTimeOffset.Parse(fixture.GetProperty("asOfUtc").GetString()!);
        var service = new PmAnalyticsV3QueryService(new TestContextFactory(options), new FixedTimeProvider(now));
        var resolution = await service.NormalizeAndResolveAsync(plan, CancellationToken.None);
        Assert.NotNull(resolution.Plan);
        return await service.ExecuteAsync(resolution.Plan, page, pageSize, CancellationToken.None);
    }

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
