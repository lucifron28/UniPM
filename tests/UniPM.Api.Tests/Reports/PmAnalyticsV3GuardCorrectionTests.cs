using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3GuardCorrectionTests
{
    private const string ProgressQuestion = "Show progress for water drinking stations in 2024";

    [Theory]
    [InlineData("Create a report listing registered assets.")]
    [InlineData("Show a complete list of registered assets.")]
    public void Report_generation_is_not_a_record_mutation(string question) =>
        Assert.False(NaturalLanguageAnalyticsQuestionGuard.IsUnsafeModelRequest(question));

    [Theory]
    [InlineData("Delete all missed fire-alarm schedules from June 2025.")]
    [InlineData("Remove the overdue inspections from 2024.")]
    [InlineData("Update fire extinguisher assets in 2024.")]
    [InlineData("Predict which fire extinguishers will fail in 2027.")]
    [InlineData("Forecast late fire-alarm inspections for 2027.")]
    [InlineData("Predict late or missed fire-alarm inspections for 2027.")]
    public async Task Unsupported_intent_precedes_subject_clarification_and_provider_calls(string question)
    {
        foreach (var mode in new[] { PmAnalyticsV3Interpreter.DeterministicParser, PmAnalyticsV3Interpreter.DeepSeek })
        {
            using var handler = new ModelHandler(ValidProgress());
            using var interpreter = CreateInterpreter(handler);
            var response = await interpreter.InterpretAsync(question, CancellationToken.None, mode);
            Assert.Equal("Unsupported", response.Status);
            Assert.Null(response.Plan);
            Assert.Empty(response.ClarificationFields);
            Assert.Equal(0, handler.Calls);
        }
    }

    [Theory]
    [InlineData("During the 2024 PM cycle year, what percentage of water drinking station inspections has been finished, whether timely or late?")]
    [InlineData("Show water drinking station completion progress in 2024, whether on time or late.")]
    public async Task Inclusive_completion_wording_preserves_valid_model_plan(string question)
    {
        var proposal = ValidProgress();
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);
        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail}");
        Assert.Equal("DeepSeek", response.Interpreter);
        Assert.NotNull(response.Plan);
        Assert.Equal(JsonSerializer.Serialize(proposal.Plan), JsonSerializer.Serialize(response.Plan));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("higher", PmAnalyticsV3NumericOperator.GreaterThanOrEqual)]
    [InlineData("lower", PmAnalyticsV3NumericOperator.LessThanOrEqual)]
    public async Task Inclusive_threshold_wording_preserves_operator_and_value(string direction, PmAnalyticsV3NumericOperator op)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse("Show on-time compliance for fire extinguishers in 2024", DateTimeOffset.UtcNow);
        proposal = proposal with
        {
            Plan = proposal.Plan! with
            {
                AggregateFilters = [new(PmAnalyticsV3Catalog.OnTimeCompliancePercent, op, 80)]
            }
        };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);
        var thresholdQuestion = $"Return 2024 fire-extinguisher on-time compliance only where the percentage is 80 or {direction}.";
        var thresholdMatches = PmAnalyticsV3QuestionParsing.FindThresholds(thresholdQuestion);
        Assert.Single(thresholdMatches);
        var expectedThresholds = PmAnalyticsV3RuleParser.ResolveThresholds(
            thresholdQuestion,
            [PmAnalyticsV3Catalog.OnTimeCompliancePercent],
            out var unresolvedThreshold);
        Assert.False(unresolvedThreshold);
        Assert.Single(expectedThresholds);
        var response = await interpreter.InterpretAsync(
            thresholdQuestion,
            CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail}");
        Assert.NotNull(response.Plan);
        var threshold = Assert.Single(response.Plan.AggregateFilters);
        Assert.Equal(op, threshold.Operator);
        Assert.Equal(80, threshold.Value);
        Assert.Equal([PmAnalyticsV3Catalog.OnTimeCompliancePercent], response.Plan.Measures);
    }

    [Theory]
    [InlineData("List fire-extinguisher inspections in department Alpha or building Beta in 2024")]
    [InlineData("Show late or missed fire-extinguisher inspections in 2024")]
    [InlineData("Show fire-extinguisher compliance in 2024 for department 80 or 90")]
    public async Task Actual_filter_disjunction_still_requires_clarification(string question)
    {
        using var handler = new ModelHandler(ValidProgress());
        using var interpreter = CreateInterpreter(handler);
        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.Equal("NeedsClarification", response.Status);
        Assert.Equal("BooleanFilterNeedsClarification", response.Code);
        Assert.Null(response.Plan);
    }

    [Fact]
    public async Task Unclassifiable_schema_violation_remains_invalid_provider_output()
    {
        var proposal = ValidProgress();
        proposal = proposal with { Plan = proposal.Plan! with { SchemaVersion = 99 } };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);
        var error = await Assert.ThrowsAsync<NaturalLanguageAnalyticsProviderException>(() =>
            interpreter.InterpretAsync(ProgressQuestion, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek));
        Assert.Equal(NaturalLanguageAnalyticsProviderFailure.InvalidOutput, error.Failure);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Inclusive_threshold_does_not_allow_a_reversed_model_comparison()
    {
        var proposal = PmAnalyticsV3RuleParser.Parse("Show on-time compliance for fire extinguishers in 2024", DateTimeOffset.UtcNow);
        proposal = proposal with
        {
            Plan = proposal.Plan! with
            {
                AggregateFilters = [new(PmAnalyticsV3Catalog.OnTimeCompliancePercent, PmAnalyticsV3NumericOperator.LessThanOrEqual, 80)]
            }
        };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);
        var response = await interpreter.InterpretAsync(
            "Show 2024 fire-extinguisher on-time compliance where the percentage is 80 or higher.",
            CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.Equal("NeedsClarification", response.Status);
        Assert.Contains("Thresholds", response.ClarificationFields);
    }

    [Fact]
    public async Task Fake_model_guard_preserves_a_complete_thousand_count_threshold()
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Show scheduled fire-extinguisher inspections by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        proposal = proposal with
        {
            Plan = proposal.Plan! with
            {
                AggregateFilters =
                [
                    new PmAnalyticsV3AggregateFilter(
                        PmAnalyticsV3Catalog.ScheduledCount,
                        PmAnalyticsV3NumericOperator.GreaterThanOrEqual,
                        1000m)
                ]
            }
        };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(
            "Show scheduled fire-extinguisher inspections by department with at least 1,000 in 2026",
            CancellationToken.None,
            PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail} Plan: {JsonSerializer.Serialize(response.Plan)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.ScheduledCount], plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        var threshold = Assert.Single(plan.AggregateFilters);
        Assert.Equal(PmAnalyticsV3Catalog.ScheduledCount, threshold.Measure);
        Assert.Equal(PmAnalyticsV3NumericOperator.GreaterThanOrEqual, threshold.Operator);
        Assert.Equal(1000m, threshold.Value);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal([PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.ScheduledCount], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Fake_model_guard_drops_partial_numeric_value_and_clarifies_malformed_threshold()
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Show scheduled fire-extinguisher inspections by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        proposal = proposal with
        {
            Plan = proposal.Plan! with
            {
                AggregateFilters =
                [
                    new PmAnalyticsV3AggregateFilter(
                        PmAnalyticsV3Catalog.ScheduledCount,
                        PmAnalyticsV3NumericOperator.GreaterThanOrEqual,
                        1m)
                ]
            }
        };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(
            "Show scheduled fire-extinguisher inspections by department with at least 1,00 in 2026",
            CancellationToken.None,
            PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(response.Status == "NeedsClarification", JsonSerializer.Serialize(response));
        Assert.Contains("Thresholds", response.ClarificationFields);
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.ScheduledCount], plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal([PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.ScheduledCount], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Count fire-extinguisher inspections finished before the PM deadline by department in 2026.")]
    [InlineData("Ilang fire-extinguisher inspections ang natapos bago ang deadline by department in 2026?")]
    [InlineData("Count fire-extinguisher inspections na finished before the PM deadline by department in 2026.")]
    public async Task Fake_model_guard_preserves_catalog_measure_for_bounded_on_time_paraphrases(string question)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count completed on-time fire-extinguisher inspections by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail} Plan: {JsonSerializer.Serialize(response.Plan)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.CompletedOnTimeCount], plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.AggregateFilters);
        Assert.Equal([PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.CompletedOnTimeCount], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("How many fire-extinguisher inspections were never completed by the PM deadline in 2026, by department?", PmAnalyticsV3Catalog.MissedCount)]
    [InlineData("How many fire-extinguisher inspections finished after the due date in 2026, by department?", PmAnalyticsV3Catalog.CompletedLateCount)]
    [InlineData("How many fire-extinguisher inspections na-late pero natapos in 2026 by department?", PmAnalyticsV3Catalog.CompletedLateCount)]
    [InlineData("Ilan fire-extinguisher inspections ang hindi pa tapos matapos ang deadline in 2026 by department?", PmAnalyticsV3Catalog.MissedCount)]
    public async Task Fake_model_guard_keeps_missed_and_completed_late_distinct(string question, string expectedMeasure)
    {
        var proposalPhrase = expectedMeasure == PmAnalyticsV3Catalog.MissedCount
            ? "missed fire-extinguisher inspections"
            : "completed-late fire-extinguisher inspections";
        var proposal = PmAnalyticsV3RuleParser.Parse(
            $"Count {proposalPhrase} by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail} Plan: {JsonSerializer.Serialize(response.Plan)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([expectedMeasure], plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal([PmAnalyticsV3Catalog.Department, expectedMeasure], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Count fire-extinguisher inspections that need repair by department in 2026.")]
    [InlineData("Count fire-extinguisher inspections with may sira by department in 2026.")]
    public async Task Fake_model_guard_does_not_turn_repair_or_ambiguous_damage_into_saved_findings(string question)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count non-operational fire-extinguisher inspection findings by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(response.Status == "NeedsClarification", JsonSerializer.Serialize(response));
        Assert.Contains("Measures", response.ClarificationFields);
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Empty(plan.Measures);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Count fire-extinguisher inspection findings found not working by department in 2026.")]
    [InlineData("Count fire-extinguisher inspection findings hindi gumagana by department in 2026.")]
    public async Task Fake_model_guard_preserves_contextual_non_operational_finding_paraphrases(string question)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count non-operational fire-extinguisher inspection findings by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(
            response.Status == "Valid",
            $"{response.Code}: {string.Join(", ", response.ClarificationFields)}. {response.Detail} Plan: {JsonSerializer.Serialize(response.Plan)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.NonOperationalCount], plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal([PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.NonOperationalCount], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Fake_model_guard_preserves_an_explicit_bounded_outcome_comparison_with_another_measure()
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count completed on-time fire-extinguisher inspections by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(
            "Compare scheduled fire-extinguisher inspections with those finished before the PM deadline by department in 2026",
            CancellationToken.None,
            PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(response.Status == "Valid", JsonSerializer.Serialize(response));
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal([PmAnalyticsV3Catalog.ScheduledCount, PmAnalyticsV3Catalog.CompletedOnTimeCount], plan.Measures);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal(
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.ScheduledCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
            plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Compare completed fire-extinguisher inspections with those finished before the PM deadline by department in 2026.")]
    [InlineData("Compare completed fire-extinguisher inspections versus those finished before the PM deadline by department in 2026.")]
    [InlineData("Count completed fire-extinguisher inspections and inspections finished before the PM deadline by department in 2026.")]
    [InlineData("Ihambing ang mga inspeksyon ng pamatay-sunog na natapos sa mga natapos bago ang PM deadline ayon sa departamento noong 2026.")]
    [InlineData("Ilang inspeksyon ng pamatay-sunog ang natapos at natapos bago ang PM deadline ayon sa departamento noong 2026?")]
    [InlineData("I-compare ang completed fire-extinguisher inspections sa mga natapos before the PM deadline by department in 2026.")]
    [InlineData("Count completed fire-extinguisher inspections at inspections na finished before the PM deadline by department in 2026.")]
    public void Deterministic_parser_preserves_explicit_completed_and_on_time_comparison(string question)
    {
        var response = PmAnalyticsV3RuleParser.Parse(
            question,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(response.Status == "Valid", $"{response.Code}: {string.Join(", ", response.ClarificationFields)} {JsonSerializer.Serialize(response)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal(
            [PmAnalyticsV3Catalog.CompletedCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
            plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal(
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.CompletedCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
            plan.Columns);
    }

    [Theory]
    [InlineData("Compare completed fire-extinguisher inspections with those finished before the PM deadline by department in 2026.")]
    [InlineData("Compare completed fire-extinguisher inspections versus those finished before the PM deadline by department in 2026.")]
    [InlineData("Count completed fire-extinguisher inspections and inspections finished before the PM deadline by department in 2026.")]
    [InlineData("Ihambing ang mga inspeksyon ng pamatay-sunog na natapos sa mga natapos bago ang PM deadline ayon sa departamento noong 2026.")]
    [InlineData("Ilang inspeksyon ng pamatay-sunog ang natapos at natapos bago ang PM deadline ayon sa departamento noong 2026?")]
    [InlineData("I-compare ang completed fire-extinguisher inspections sa mga natapos before the PM deadline by department in 2026.")]
    [InlineData("Count completed fire-extinguisher inspections at inspections na finished before the PM deadline by department in 2026.")]
    public async Task Fake_model_guard_preserves_explicit_completed_and_on_time_comparison(string question)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count completed fire-extinguisher inspections by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        proposal = proposal with
        {
            Plan = proposal.Plan! with
            {
                Measures = [PmAnalyticsV3Catalog.CompletedCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
                Columns =
                [
                    PmAnalyticsV3Catalog.Department,
                    PmAnalyticsV3Catalog.CompletedCount,
                    PmAnalyticsV3Catalog.CompletedOnTimeCount
                ]
            }
        };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(
            question,
            CancellationToken.None,
            PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(response.Status == "Valid", $"{response.Code}: {string.Join(", ", response.ClarificationFields)} {JsonSerializer.Serialize(response)}");
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, plan.Subject);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, plan.Mode);
        Assert.Equal(new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2026), plan.Period);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal(
            [PmAnalyticsV3Catalog.CompletedCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
            plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Empty(plan.BaseFilters);
        Assert.Empty(plan.MatchingFilters);
        Assert.Empty(plan.AggregateFilters);
        Assert.Empty(plan.OrderBy);
        Assert.Null(plan.Top);
        Assert.Equal(PmAnalyticsV3Presentation.Table, plan.Presentation);
        Assert.Equal(
            [PmAnalyticsV3Catalog.Department, PmAnalyticsV3Catalog.CompletedCount, PmAnalyticsV3Catalog.CompletedOnTimeCount],
            plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("Count fire-extinguisher inspection findings when the mobile app is not working by department in 2026.")]
    [InlineData("Count fire-extinguisher inspection findings when the QR scanner is not working by department in 2026.")]
    [InlineData("Ilang inspeksyon ng pamatay-sunog ang naitala habang hindi gumagana ang mobile app by department in 2026?")]
    [InlineData("Ilang inspeksyon ng pamatay-sunog ang naitala habang hindi gumagana ang QR scanner by department in 2026?")]
    public async Task Fake_model_guard_does_not_treat_app_or_scanner_failure_as_equipment_finding(string question)
    {
        var proposal = PmAnalyticsV3RuleParser.Parse(
            "Count non-operational fire-extinguisher inspection findings by department in 2026",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal("Valid", proposal.Status);
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);

        var response = await interpreter.InterpretAsync(question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);

        Assert.True(response.Status == "NeedsClarification", JsonSerializer.Serialize(response));
        Assert.Contains("Measures", response.ClarificationFields);
        var plan = Assert.IsType<PmAnalyticsV3ReportPlan>(response.Plan);
        Assert.Empty(plan.Measures);
        Assert.DoesNotContain(PmAnalyticsV3Catalog.NonOperationalCount, plan.Measures);
        Assert.Equal(["fire-extinguisher"], plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.GroupBy);
        Assert.Equal([PmAnalyticsV3Catalog.Department], plan.Columns);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Invalid_grouping_is_discarded_and_returns_recoverable_fields()
    {
        var proposal = ValidProgress();
        proposal = proposal with { Plan = proposal.Plan! with { GroupBy = ["assetId"], Columns = ["assetId", "progressPercent"], Presentation = PmAnalyticsV3Presentation.Table } };
        using var handler = new ModelHandler(proposal);
        using var interpreter = CreateInterpreter(handler);
        var response = await interpreter.InterpretAsync(ProgressQuestion, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.Equal("NeedsClarification", response.Status);
        Assert.Equal("InvalidPlanNeedsClarification", response.Code);
        Assert.Null(response.Plan);
        Assert.Contains("GroupBy", response.ClarificationFields);
        Assert.Equal("DeepSeek", response.Interpreter);
        Assert.Equal(1, handler.Calls);
    }

    private static PmAnalyticsV3ModelOutput ValidProgress() =>
        PmAnalyticsV3RuleParser.Parse(ProgressQuestion, DateTimeOffset.UtcNow);

    private static PmAnalyticsV3QuestionInterpreter CreateInterpreter(HttpMessageHandler handler)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"guard-corrections-{Guid.NewGuid():N}").Options;
        var query = new PmAnalyticsV3QueryService(new ContextFactory(options), TimeProvider.System);
        return new PmAnalyticsV3QuestionInterpreter(new OptionsMonitor(), new HostEnvironment(),
            TimeProvider.System, query, new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { [DeepSeekNaturalLanguageAnalyticsModelClient.ApiKeyEnvironmentVariable] = "synthetic-test-key" }).Build(), handler);
    }

    private sealed class ContextFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
    private sealed class OptionsMonitor : IOptionsMonitor<NaturalLanguageAnalyticsOptions>
    {
        public NaturalLanguageAnalyticsOptions CurrentValue { get; } = new() { V3DeepSeekEnabled = true };
        public NaturalLanguageAnalyticsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NaturalLanguageAnalyticsOptions, string?> listener) => null;
    }
    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UniPM.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class ModelHandler(PmAnalyticsV3ModelOutput output) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var content = JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } }), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
