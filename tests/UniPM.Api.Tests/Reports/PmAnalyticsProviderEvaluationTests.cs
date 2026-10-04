using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniPM.Api.Features.Reports;
using EvalProgram = UniPM.PmAnalytics.InterpretationEval.Program;
using Xunit;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsProviderEvaluationTests
{
    private const string V2DevSha256 = "f98d505d6c2ac2f1b3372109e50b949fd2211b7b36af3104536451d4406f2533";
    private const string V2HeldoutSha256 = "c39307b5e4f76cf84b4a7d6880e180c2a652fa74bad29f741c8f07d1123c908b";

    [Fact]
    public async Task V2_dev_selection_checks_the_manifest_hash_and_distribution()
    {
        var selection = await EvalProgram.ReadCasesAsync(FindV2DatasetPath("dev"), "dev", "v2");

        Assert.Equal(V2DevSha256, selection.Sha256);
        Assert.Equal(60, selection.Cases.Count);
        Assert.Equal(20, selection.Cases.Select(item => item.FamilyId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task V2_heldout_fixture_loader_selects_thirty_cases_when_called_explicitly()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var fixture = await WriteSplitFixtureAsync(temporaryDirectory, "heldout", SyntheticCases("heldout"));

            var selection = await EvalProgram.ReadV2SplitFileAsync(fixture.DatasetPath, "heldout", fixture.Sha256);

            Assert.Equal(30, selection.Cases.Count);
            Assert.Equal(10, selection.Cases.Select(item => item.FamilyId).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(fixture.Sha256, selection.Sha256);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void V2_family_assignments_match_the_frozen_manifest()
    {
        var dev = ReadCaseMetadata(FindV2DatasetPath("dev"));
        var heldout = ReadCaseMetadata(FindV2DatasetPath("heldout"));
        var expectedDev = FamilyIds("F", 1, 5)
            .Concat(FamilyIds("C", 1, 5))
            .Concat(FamilyIds("U", 1, 5))
            .Concat(FamilyIds("A", 1, 5))
            .ToHashSet(StringComparer.Ordinal);
        var expectedHeldout = FamilyIds("F", 6, 7)
            .Concat(FamilyIds("C", 6, 10))
            .Concat(FamilyIds("U", 6, 7))
            .Concat(FamilyIds("A", 6, 6))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(expectedDev.SetEquals(dev.Select(item => item.FamilyId)));
        Assert.True(expectedHeldout.SetEquals(heldout.Select(item => item.FamilyId)));
    }

    [Fact]
    public void V2_case_ids_remain_unique_across_both_split_files()
    {
        var cases = ReadCaseMetadata(FindV2DatasetPath("dev"))
            .Concat(ReadCaseMetadata(FindV2DatasetPath("heldout")))
            .ToArray();

        Assert.Equal(90, cases.Length);
        Assert.Equal(90, cases.Select(item => item.CaseId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void V2_split_files_keep_expected_language_and_status_distributions()
    {
        var cases = ReadCaseMetadata(FindV2DatasetPath("dev"))
            .Concat(ReadCaseMetadata(FindV2DatasetPath("heldout")))
            .ToArray();

        AssertSplitDistribution(cases, "dev", 20, 15, 15, 30);
        AssertSplitDistribution(cases, "heldout", 10, 6, 15, 9);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("directory")]
    public async Task V2_dev_loader_ignores_missing_malformed_or_unreadable_heldout_file(string heldoutState)
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var fixture = await WriteSplitFixtureAsync(temporaryDirectory, "dev", SyntheticCases("dev"));
            var heldoutPath = Path.Combine(temporaryDirectory, "heldout.jsonl");
            if (heldoutState == "malformed")
            {
                await File.WriteAllTextAsync(heldoutPath, "not-json");
            }
            else if (heldoutState == "directory")
            {
                Directory.CreateDirectory(heldoutPath);
            }

            var selection = await EvalProgram.ReadV2SplitFileAsync(fixture.DatasetPath, "dev", fixture.Sha256);

            Assert.Equal(60, selection.Cases.Count);
            Assert.All(selection.Cases, item => Assert.Equal("dev", item.Split));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task V2_dev_selection_never_sends_heldout_questions_to_interpreter()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var devFixture = await WriteSplitFixtureAsync(temporaryDirectory, "dev", SyntheticCases("dev"));
            _ = await WriteSplitFixtureAsync(temporaryDirectory, "heldout", SyntheticCases("heldout"));
            var selection = await EvalProgram.ReadV2SplitFileAsync(devFixture.DatasetPath, "dev", devFixture.Sha256);
            var interpreter = new RecordingInterpreter();

            _ = await EvalProgram.EvaluateAsync(interpreter, selection.Cases);

            Assert.Equal(60, interpreter.Questions.Count);
            Assert.True(interpreter.Questions.All(question => question.StartsWith("DEV_FIXTURE ", StringComparison.Ordinal)));
            Assert.DoesNotContain(interpreter.Questions, question => question.Contains("HELDOUT_SENTINEL", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task V2_selected_split_hash_mismatch_fails_closed()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            var fixture = await WriteSplitFixtureAsync(
                temporaryDirectory,
                "dev",
                SyntheticCases("dev"),
                corruptSelectedManifestHash: true);

            var exception = await Assert.ThrowsAsync<EvalProgram.EvaluationSetupException>(
                () => EvalProgram.ReadV2SplitFileAsync(fixture.DatasetPath, "dev", fixture.Sha256));

            Assert.Equal("DatasetDigestMismatch", exception.Code);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void V2_family_cannot_be_assigned_to_more_than_one_split()
    {
        var familySplits = new Dictionary<string, string>(StringComparer.Ordinal);
        EvalProgram.RecordFamilySplit("nla2-F01", "dev", familySplits);

        var exception = Assert.Throws<EvalProgram.EvaluationSetupException>(
            () => EvalProgram.RecordFamilySplit("nla2-F01", "heldout", familySplits));

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
        Assert.Equal("bounded-transient-v1", report.V2.ProviderRun.RetryPolicyVersion);
        Assert.Equal(1, report.V2.ProviderRun.FirstRetryBackoffSeconds);
        Assert.Equal(2, report.V2.ProviderRun.SecondRetryBackoffSeconds);
        Assert.Equal(0, report.V2.ProviderRun.MinimumJitterMilliseconds);
        Assert.Equal(250, report.V2.ProviderRun.MaximumJitterMilliseconds);
        Assert.Equal(5, report.V2.ProviderRun.MaximumRetryAfterSeconds);
        Assert.Equal(60, report.V2.ProviderRun.MaximumLogicalCalls);
        Assert.Equal(180, report.V2.ProviderRun.MaximumHttpAttempts);
        Assert.Equal(2, report.V2.ProviderRun.MaximumRetriesPerLogicalCall);
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

    [Fact]
    public void Recovered_transient_attempts_keep_provider_cost_incomplete_without_usage_metadata()
    {
        var run = new NaturalLanguageAnalyticsModelClientRunSnapshot(
            LogicalCalls: 1,
            Attempts: 2,
            Retries: 1,
            HttpSuccessResponses: 1,
            ResponsesWithText: 1,
            ProviderFailures: 1,
            TerminalFailureCode: null,
            Calls:
            [
                new NaturalLanguageAnalyticsModelCallObservation(
                    false,
                    false,
                    null,
                    12,
                    null,
                    null,
                    "Http5xxServerError",
                    503),
                new NaturalLanguageAnalyticsModelCallObservation(
                    true,
                    true,
                    new NaturalLanguageAnalyticsProviderUsage(100, 20, 2_000),
                    18,
                    "deepseek-flash",
                    null,
                    null)
            ]);

        var accounting = EvalProgram.BuildApiUsageAccounting("deepseek", run);

        Assert.False(accounting.BaseUsageComplete);
        Assert.Null(accounting.EstimatedCostUsd);
        Assert.Equal("incomplete", accounting.CostStatus);
        Assert.Equal(1, accounting.Totals.PromptTokenResponses);
        Assert.Null(accounting.Totals.PromptTokens);

        var item = ValidCase("V01", "Progress", presentation: "Count");
        var result = Result(
            PmAnalyticsInterpretationStatus.Valid,
            Plan(PmAnalyticsMetric.Progress),
            presentation: PmAnalyticsPresentation.Count) with
        {
            Usage = new NaturalLanguageAnalyticsProviderUsage(100, 20, 2_000)
        };
        var outcome = EvalProgram.ScoreCase(
            item,
            result,
            18);
        var report = BuildReport(
            [item],
            [outcome],
            new EvalProgram.ProviderRunConfiguration("deepseek", "deepseek-flash", null, "disabled", 0),
            run);

        Assert.Equal(2, report.Execution.ProviderAttempts);
        Assert.Equal(0, report.Execution.ProviderErrors);
        Assert.True(report.V2!.EvaluationComplete);
        Assert.Equal(1, report.V2.ProviderRun!.LogicalCalls);
        Assert.Equal(2, report.V2.ProviderRun.ActualAttempts);
        Assert.Equal(1, report.V2.ProviderRun.Retries);
        Assert.Equal(1, report.V2.ProviderRun.ProviderFailures);
        Assert.False(report.V2.ProviderRun.UsageComplete);
        Assert.Equal("incomplete", report.V2.ProviderRun.CostStatus);
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
            new EvalProgram.DatasetSelection(cases, V2DevSha256),
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
            calls.Length,
            0,
            calls.Count(call => call.HttpSucceeded),
            calls.Count(call => call.HttpSucceeded && call.HasText),
            calls.Count(call => !call.HttpSucceeded),
            null,
            calls);

    private static EvalProgram.EvaluationCase[] SyntheticCases(string split)
    {
        var classByFamily = split == "dev"
            ? Enumerable.Repeat("valid", 5)
                .Concat(Enumerable.Repeat("needs-clarification", 5))
                .Concat(Enumerable.Repeat("unsupported", 5))
                .Concat(Enumerable.Repeat("adversarial", 5))
                .ToArray()
            : Enumerable.Repeat("valid", 2)
                .Concat(Enumerable.Repeat("needs-clarification", 5))
                .Concat(Enumerable.Repeat("unsupported", 2))
                .Concat(Enumerable.Repeat("adversarial", 1))
                .ToArray();
        var languages = new[] { "en", "fil", "taglish" };
        var metrics = new[] { "Progress", "OnTimeCompliance", "CompletedLate", "NonOperational" };
        var categories = new[]
        {
            "fire-extinguisher", "fire-alarm", "emergency-light", "water-drinking-station"
        };
        var cases = new List<EvalProgram.EvaluationCase>();
        for (var familyIndex = 0; familyIndex < classByFamily.Length; familyIndex++)
        {
            var familyId = $"{(split == "dev" ? "D" : "H")}{familyIndex + 1:D2}";
            var caseClass = classByFamily[familyIndex];
            foreach (var language in languages)
            {
                var caseId = $"{familyId}-{language}";
                var status = caseClass switch
                {
                    "valid" => "Valid",
                    "needs-clarification" => "NeedsClarification",
                    _ => "Unsupported"
                };
                var metric = metrics[familyIndex % metrics.Length];
                var category = categories[familyIndex % categories.Length];
                var pmCycle = category is "fire-alarm" or "emergency-light"
                    ? "2026-06"
                    : "2026-11";
                var expected = status switch
                {
                    "Valid" => new EvalProgram.ExpectedLabel(
                        status,
                        new EvalProgram.ExpectedPlan(
                            metric,
                            category,
                            pmCycle,
                            null,
                            "None"),
                        [],
                        metric is "CompletedLate" or "NonOperational" ? "Count" : "Percent"),
                    "NeedsClarification" => new EvalProgram.ExpectedLabel(status, null, ["Metric"], null),
                    _ => new EvalProgram.ExpectedLabel(status, null, [], null)
                };
                var isControl = language == "en"
                    && caseClass == "valid"
                    && (split == "dev" ? familyIndex < 3 : familyIndex == 0);
                var questionPrefix = split == "dev" ? "DEV_FIXTURE" : "HELDOUT_SENTINEL";
                cases.Add(new EvalProgram.EvaluationCase(
                    caseId,
                    familyId,
                    split,
                    language,
                    caseClass,
                    $"{questionPrefix} {caseId}",
                    expected,
                    isControl ? "strict-template-control" : null));
            }
        }

        return cases.ToArray();
    }

    private static async Task<(string DatasetPath, string Sha256)> WriteSplitFixtureAsync(
        string directory,
        string split,
        IReadOnlyList<EvalProgram.EvaluationCase> cases,
        bool corruptSelectedManifestHash = false)
    {
        Directory.CreateDirectory(directory);
        var fileName = split == "dev" ? "dev.jsonl" : "heldout.jsonl";
        var datasetPath = Path.Combine(directory, fileName);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var text = string.Join('\n', cases.Select(item => JsonSerializer.Serialize(item, options))) + "\n";
        var bytes = new UTF8Encoding(false).GetBytes(text);
        await File.WriteAllBytesAsync(datasetPath, bytes);
        var sha256 = HashSha256(bytes);

        var devPath = Path.Combine(directory, "dev.jsonl");
        var heldoutPath = Path.Combine(directory, "heldout.jsonl");
        var devSha256 = split == "dev" ? sha256 : File.Exists(devPath) ? await HashFileAsync(devPath) : new string('0', 64);
        var heldoutSha256 = split == "heldout" ? sha256 : File.Exists(heldoutPath) ? await HashFileAsync(heldoutPath) : new string('0', 64);
        if (corruptSelectedManifestHash)
        {
            if (split == "dev")
            {
                devSha256 = new string('0', 64);
            }
            else
            {
                heldoutSha256 = new string('0', 64);
            }
        }

        var manifest = string.Join('\n',
            "---",
            "id: NLA-INTERPRETATION-V2-SPLIT",
            "status: provisional",
            "---",
            string.Empty,
            $"Development cases SHA-256 (UTF-8 without BOM, LF line endings): {devSha256}",
            $"Held-out cases SHA-256 (UTF-8 without BOM, LF line endings): {heldoutSha256}",
            string.Empty);
        await File.WriteAllTextAsync(Path.Combine(directory, "split-manifest.md"), manifest, new UTF8Encoding(false));
        return (datasetPath, sha256);
    }

    private static async Task<string> HashFileAsync(string path)
        => HashSha256(await File.ReadAllBytesAsync(path));

    private static string HashSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static IReadOnlyList<CaseMetadata> ReadCaseMetadata(string path)
    {
        var cases = new List<CaseMetadata>();
        foreach (var line in File.ReadLines(path))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            cases.Add(new CaseMetadata(
                root.GetProperty("caseId").GetString()!,
                root.GetProperty("familyId").GetString()!,
                root.GetProperty("split").GetString()!,
                root.GetProperty("language").GetString()!,
                root.GetProperty("caseClass").GetString()!,
                root.GetProperty("expected").GetProperty("status").GetString()!));
        }

        return cases;
    }

    private static void AssertSplitDistribution(
        IReadOnlyList<CaseMetadata> cases,
        string split,
        int languageCount,
        int validCount,
        int clarificationCount,
        int unsupportedCount)
    {
        var selected = cases.Where(item => item.Split == split).ToArray();
        Assert.Equal(languageCount * 3, selected.Length);
        Assert.Equal(validCount, selected.Count(item => item.Status == "Valid"));
        Assert.Equal(clarificationCount, selected.Count(item => item.Status == "NeedsClarification"));
        Assert.Equal(unsupportedCount, selected.Count(item => item.Status == "Unsupported"));

        var expectedValidPerLanguage = validCount / 3;
        var expectedClarificationPerLanguage = clarificationCount / 3;
        var expectedUnsupportedPerLanguage = unsupportedCount / 3;
        foreach (var language in new[] { "en", "fil", "taglish" })
        {
            var languageCases = selected.Where(item => item.Language == language).ToArray();
            Assert.Equal(languageCount, languageCases.Length);
            Assert.Equal(expectedValidPerLanguage, languageCases.Count(item => item.Status == "Valid"));
            Assert.Equal(expectedClarificationPerLanguage, languageCases.Count(item => item.Status == "NeedsClarification"));
            Assert.Equal(expectedUnsupportedPerLanguage, languageCases.Count(item => item.Status == "Unsupported"));
        }
    }

    private static IEnumerable<string> FamilyIds(string prefix, int first, int last)
        => Enumerable.Range(first, last - first + 1).Select(number => $"nla2-{prefix}{number:D2}");

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetFullPath(Path.GetTempPath()), $"unipm-v2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var prefix = tempRoot.EndsWith(Path.DirectorySeparatorChar)
            ? tempRoot
            : tempRoot + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Temporary fixture path escaped the system temp directory.");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static string FindV2DatasetPath(string split)
    {
        var fileName = split switch
        {
            "dev" => "dev.jsonl",
            "heldout" => "heldout.jsonl",
            _ => throw new ArgumentOutOfRangeException(nameof(split))
        };
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
                    fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository v2 evaluation dataset.");
    }

    private sealed record CaseMetadata(
        string CaseId,
        string FamilyId,
        string Split,
        string Language,
        string CaseClass,
        string Status);

    private sealed class RecordingInterpreter : INaturalLanguageAnalyticsInterpreter
    {
        public List<string> Questions { get; } = [];

        public Task<PmAnalyticsInterpretationResult> InterpretAsync(
            string question,
            CancellationToken cancellationToken)
        {
            Questions.Add(question);
            return Task.FromResult(new PmAnalyticsInterpretationResult(
                PmAnalyticsInterpretationStatus.Unsupported,
                null,
                [],
                null,
                null));
        }
    }
}
