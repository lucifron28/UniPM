using System.Text.Json;
using UniPM.Api.Features.Reports;
using EvalProgram = UniPM.PmAnalytics.InterpretationEval.Program;
using Xunit;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsProviderEvaluationTests
{
    private const string V2Sha256 = "510bf8c4998f33828b2b58f7a00d74d47c32333ba6c1ccaa184e1bacdee3cfa6";

    [Fact]
    public async Task V2_dev_selection_checks_the_manifest_hash_and_distribution()
    {
        var selection = await EvalProgram.ReadCasesAsync(FindV2DatasetPath(), "dev", "v2");

        Assert.Equal(V2Sha256, selection.Sha256);
        Assert.Equal(60, selection.Cases.Count);
        Assert.Equal(20, selection.Cases.Select(item => item.FamilyId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void V2_family_cannot_be_assigned_to_more_than_one_split()
    {
        var familySplits = new Dictionary<string, string>(StringComparer.Ordinal);
        EvalProgram.RecordFamilySplit("F01", "dev", familySplits);

        var exception = Assert.Throws<EvalProgram.EvaluationSetupException>(
            () => EvalProgram.RecordFamilySplit("F01", "heldout", familySplits));

        Assert.Equal("FamilyCrossesSplits", exception.Code);
    }

    [Fact]
    public void Heldout_v2_is_rejected_before_any_provider_configuration_is_needed()
    {
        var exception = Assert.Throws<EvalProgram.EvaluationSetupException>(
            () => EvalProgram.ValidateDatasetExecutionAllowed("v2", "heldout"));

        Assert.Equal("V2HeldoutNotAuthorized", exception.Code);
        EvalProgram.ValidateDatasetExecutionAllowed("v2", "dev");
    }

    [Fact]
    public void Cloud_modes_reject_v1_before_dataset_or_credentials_are_resolved()
    {
        var accepted = EvalProgram.TryParseArguments(
            ["--split", "dev", "--mode", "gemini", "--dataset-version", "v1", "--source-sha", new string('a', 40)],
            out _,
            out var error);

        Assert.False(accepted);
        Assert.Contains("require --dataset-version v2", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Scores_keep_presentation_separate_and_use_expected_class_denominators()
    {
        var valid = ValidCase("V01", "Progress", presentation: "Count");
        var clarification = ClarificationCase("C01");
        var unsupported = UnsupportedCase("U01", "unsupported");
        var adversarial = UnsupportedCase("A01", "adversarial");
        var plan = Plan(PmAnalyticsMetric.Progress);
        var outcomes = new[]
        {
            EvalProgram.ScoreCase(valid, Result(PmAnalyticsInterpretationStatus.Valid, plan, presentation: PmAnalyticsPresentation.Percent), 10),
            EvalProgram.ScoreCase(clarification, Result(
                PmAnalyticsInterpretationStatus.NeedsClarification,
                clarificationFields: [PmAnalyticsClarificationField.Month]), 12),
            EvalProgram.ScoreCase(unsupported, Result(PmAnalyticsInterpretationStatus.Unsupported), 14),
            EvalProgram.ScoreCase(adversarial, Result(
                PmAnalyticsInterpretationStatus.Valid,
                plan,
                presentation: PmAnalyticsPresentation.Percent), 16)
        };

        var report = BuildReport([valid, clarification, unsupported, adversarial], outcomes);

        Assert.Equal(new EvalProgram.MetricScore(1, 1, 1m), report.Scores.CompletePlanAccuracyOnExpectedValid);
        Assert.Equal(new EvalProgram.MetricScore(0, 1, 0m), report.Scores.ValidFieldAccuracy["presentation"]);
        Assert.Equal(new EvalProgram.MetricScore(1, 1, 1m), report.Scores.ExactClarificationFieldsOnExpectedClarification);
        Assert.Equal(new EvalProgram.MetricScore(1, 2, 0.5m), report.Scores.UnsupportedRejectionOnExpectedUnsupported);
        Assert.Equal(new EvalProgram.MetricScore(0, 1, 0m), report.V2!.AdversarialRejection);
        Assert.Equal(1, report.Scores.NonValidExecutableOutputCount);
    }

    [Fact]
    public void Not_executed_cases_are_excluded_from_scores_and_latency_but_remain_planned()
    {
        var completed = ValidCase("V01", "Progress", presentation: "Count");
        var pending = ValidCase("V02", "OnTimeCompliance", presentation: "Percent");
        var outcome = EvalProgram.ScoreCase(
            completed,
            Result(PmAnalyticsInterpretationStatus.Valid, Plan(PmAnalyticsMetric.Progress), presentation: PmAnalyticsPresentation.Count),
            10);
        var report = BuildReport(
            [completed, pending],
            [outcome, EvalProgram.CaseOutcome.NotExecutedCase(pending)]);

        Assert.Equal(new EvalProgram.MetricScore(1, 1, 1m), report.Scores.StatusAccuracy);
        Assert.Equal(1, report.Latency.SampleCount);
        Assert.Equal(2, report.V2!.PlannedCases);
        Assert.Equal(1, report.V2.EvaluatedCases);
        Assert.Equal(1, report.V2.NotExecutedCases);
        Assert.False(report.V2.EvaluationComplete);
    }

    [Fact]
    public void Successful_http_usage_for_invalid_model_output_is_retained_and_does_not_make_run_incomplete()
    {
        var item = ValidCase("V01", "Progress", presentation: "Count");
        var usage = new NaturalLanguageAnalyticsProviderUsage(
            PromptTokens: 100,
            CompletionTokens: 20,
            DurationNanoseconds: 5_000,
            ReasoningTokens: 7);
        var run = Snapshot(new NaturalLanguageAnalyticsModelCallObservation(
            HttpSucceeded: true,
            HasText: true,
            Usage: usage,
            LatencyMilliseconds: 25,
            ReportedModelVersion: "gemini-3.8-flash",
            SystemFingerprint: "fp-1",
            SafeErrorCode: null));
        var outcome = EvalProgram.CaseOutcome.Failure(item, "InvalidOutput", evaluatorError: null, 25);
        var report = BuildReport(
            [item],
            [outcome],
            new EvalProgram.ProviderRunConfiguration("gemini", "gemini-3.8-flash", "v1beta", "low", null),
            run);

        Assert.Equal(1, report.Execution.ProviderErrors);
        Assert.Equal(100L, report.Usage.PromptTokens);
        Assert.Equal(20L, report.Usage.CompletionTokens);
        Assert.Equal(0.00015m, report.Usage.Cost);
        Assert.True(report.V2!.EvaluationComplete);
        Assert.True(report.V2.ProviderRun!.UsageComplete);
        Assert.Equal(7L, report.V2.ProviderRun.ReasoningTokens);
    }

    [Fact]
    public void Provider_costs_use_billed_completion_and_guard_missing_or_negative_usage()
    {
        var gemini = EvalProgram.BuildApiUsageAccounting("gemini", Snapshot(new NaturalLanguageAnalyticsModelCallObservation(
            true,
            true,
            new NaturalLanguageAnalyticsProviderUsage(100, 20, 1_000, ReasoningTokens: 7),
            10,
            null,
            null,
            null)));
        var deepSeek = EvalProgram.BuildApiUsageAccounting("deepseek", Snapshot(new NaturalLanguageAnalyticsModelCallObservation(
            true,
            true,
            new NaturalLanguageAnalyticsProviderUsage(100, 20, 1_000, 40, 60, 7),
            10,
            null,
            null,
            null)));
        var missingUsage = EvalProgram.BuildApiUsageAccounting("gemini", Snapshot(new NaturalLanguageAnalyticsModelCallObservation(
            true,
            true,
            new NaturalLanguageAnalyticsProviderUsage(null, 20, 1_000),
            10,
            null,
            null,
            null)));
        var negativeUsage = EvalProgram.BuildApiUsageAccounting("deepseek", Snapshot(new NaturalLanguageAnalyticsModelCallObservation(
            true,
            true,
            new NaturalLanguageAnalyticsProviderUsage(-1, 20, 1_000),
            10,
            null,
            null,
            null)));

        Assert.Equal(0.00015m, gemini.EstimatedCostUsd);
        Assert.Equal(0.00004224m, deepSeek.EstimatedCostUsd);
        Assert.Equal(7L, deepSeek.ReasoningTokens);
        Assert.Null(missingUsage.EstimatedCostUsd);
        Assert.False(missingUsage.BaseUsageComplete);
        Assert.Equal("incomplete", missingUsage.CostStatus);
        Assert.Null(negativeUsage.EstimatedCostUsd);
        Assert.False(negativeUsage.BaseUsageComplete);
        Assert.Equal("incomplete", negativeUsage.CostStatus);
    }

    private static EvalProgram.EvaluationReport BuildReport(
        EvalProgram.EvaluationCase[] cases,
        EvalProgram.CaseOutcome[] outcomes,
        EvalProgram.ProviderRunConfiguration? provider = null,
        NaturalLanguageAnalyticsModelClientRunSnapshot? apiRun = null)
        => EvalProgram.BuildReport(
            new EvalProgram.EvaluationArguments
            {
                DatasetVersion = "v2",
                Split = "dev",
                Mode = provider?.Provider ?? "rule-based",
                SourceSha = new string('a', 40)
            },
            new EvalProgram.DatasetSelection(cases, V2Sha256),
            outcomes,
            null,
            new NaturalLanguageAnalyticsOptions(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(1),
            provider,
            apiRun);

    private static EvalProgram.EvaluationCase ValidCase(string id, string metric, string presentation)
        => new(
            id,
            id,
            "dev",
            "en",
            "valid",
            "Synthetic evaluation question",
            new EvalProgram.ExpectedLabel(
                "Valid",
                new EvalProgram.ExpectedPlan(metric, "fire-extinguisher", "2026-11", null, "None"),
                [],
                presentation),
            null);

    private static EvalProgram.EvaluationCase ClarificationCase(string id)
        => new(
            id,
            id,
            "dev",
            "en",
            "needs-clarification",
            "Synthetic clarification question",
            new EvalProgram.ExpectedLabel("NeedsClarification", null, ["Month"], null),
            null);

    private static EvalProgram.EvaluationCase UnsupportedCase(string id, string caseClass)
        => new(
            id,
            id,
            "dev",
            "en",
            caseClass,
            "Synthetic unsupported question",
            new EvalProgram.ExpectedLabel("Unsupported", null, [], null),
            null);

    private static PmAnalyticsPlan Plan(PmAnalyticsMetric metric)
        => new(metric, "fire-extinguisher", "2026-11", null, PmAnalyticsGroupBy.None);

    private static PmAnalyticsInterpretationResult Result(
        PmAnalyticsInterpretationStatus status,
        PmAnalyticsPlan? plan = null,
        PmAnalyticsPresentation? presentation = null,
        IReadOnlyList<PmAnalyticsClarificationField>? clarificationFields = null)
        => new(status, plan, clarificationFields ?? [], presentation, null);

    private static NaturalLanguageAnalyticsModelClientRunSnapshot Snapshot(
        params NaturalLanguageAnalyticsModelCallObservation[] calls)
        => new(
            calls.Length,
            calls.Count(call => call.HttpSucceeded),
            calls.Count(call => call.HttpSucceeded && call.HasText),
            calls.Count(call => !call.HttpSucceeded),
            null,
            calls);

    private static string FindV2DatasetPath()
    {
        foreach (var startingPath in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startingPath));
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "reference",
                    "evaluation",
                    "pm-analytics-interpretation",
                    "v2",
                    "cases.jsonl");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository v2 evaluation dataset.");
    }
}
