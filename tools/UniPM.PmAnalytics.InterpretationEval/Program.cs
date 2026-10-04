using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Reports;

namespace UniPM.PmAnalytics.InterpretationEval;

internal static class Program
{
    private const string V1DatasetRelativePath = "reference/evaluation/pm-analytics-interpretation/v1/cases.jsonl";
    private const string V2DatasetRelativePath = "reference/evaluation/pm-analytics-interpretation/v2/cases.jsonl";
    private const string ManifestFileName = "split-manifest.md";
    private const string V2DatasetSha256 = "510bf8c4998f33828b2b58f7a00d74d47c32333ba6c1ccaa184e1bacdee3cfa6";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        if (!TryParseArguments(args, out var arguments, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Usage);
            return 2;
        }

        if (arguments.ShowHelp)
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            ValidateDatasetExecutionAllowed(arguments.DatasetVersion, arguments.Split);
        }
        catch (EvaluationSetupException exception)
        {
            Console.Error.WriteLine($"Evaluation setup failed: {exception.Code}");
            return 2;
        }

        try
        {
            var root = FindRepositoryRoot();
            arguments.SourceSha = await GitSourceShaVerifier.VerifyAsync(
                root,
                arguments.SourceSha);
            var datasetPath = arguments.DatasetPath is null
                ? Path.Combine(root, arguments.DatasetVersion == "v2"
                    ? V2DatasetRelativePath
                    : V1DatasetRelativePath)
                : Path.GetFullPath(arguments.DatasetPath);
            var selection = await ReadCasesAsync(datasetPath, arguments.Split, arguments.DatasetVersion);
            var naturalLanguageOptions = new NaturalLanguageAnalyticsOptions
            {
                Enabled = arguments.Mode == "ollama",
                BaseAddress = arguments.BaseAddress,
                Model = arguments.Model,
                TimeoutSeconds = 60,
                MaxOutputTokens = 1024,
                MaxResponseBytes = 16 * 1024
            };

            HttpClient? client = null;
            IDisposable? apiModelClient = null;
            NaturalLanguageAnalyticsModelClientRunLedger? apiRunLedger = null;
            ProviderRunConfiguration? providerRunConfiguration = null;
            try
            {
                INaturalLanguageAnalyticsInterpreter interpreter;
                ModelMetadata? model = null;
                if (arguments.Mode == "rule-based")
                {
                    interpreter = new RuleBasedNaturalLanguageAnalyticsInterpreter();
                }
                else if (arguments.Mode == "ollama")
                {
                    var address = ValidateLoopbackAddress(arguments.BaseAddress);
                    client = new HttpClient(new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false
                    })
                    {
                        BaseAddress = address,
                        Timeout = Timeout.InfiniteTimeSpan
                    };
                    model = await ReadModelMetadataAsync(client, arguments.Model);
                    interpreter = new OllamaNaturalLanguageAnalyticsInterpreter(
                        client,
                        new FixedOptionsMonitor<NaturalLanguageAnalyticsOptions>(naturalLanguageOptions));
                }
                else
                {
                    apiRunLedger = new NaturalLanguageAnalyticsModelClientRunLedger();
                    var apiKeyEnvironmentVariable = arguments.Mode == "gemini"
                        ? "GEMINI_API_KEY"
                        : "DEEPSEEK_API_KEY";
                    var apiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new EvaluationSetupException("ProviderApiKeyMissing");
                    }

                    INaturalLanguageAnalyticsModelClient modelClient;
                    if (arguments.Mode == "gemini")
                    {
                        var geminiClient = new GeminiNaturalLanguageAnalyticsModelClient(apiKey, apiRunLedger);
                        apiModelClient = geminiClient;
                        modelClient = geminiClient;
                        providerRunConfiguration = new ProviderRunConfiguration(
                            geminiClient.Provider,
                            geminiClient.ModelId,
                            "v1beta",
                            "low",
                            null);
                    }
                    else
                    {
                        var deepSeekClient = new DeepSeekNaturalLanguageAnalyticsModelClient(apiKey, apiRunLedger);
                        apiModelClient = deepSeekClient;
                        modelClient = deepSeekClient;
                        providerRunConfiguration = new ProviderRunConfiguration(
                            deepSeekClient.Provider,
                            deepSeekClient.ModelId,
                            null,
                            "disabled",
                            0d);
                    }

                    interpreter = new ModelNaturalLanguageAnalyticsInterpreter(modelClient);
                }

                var startedAt = DateTimeOffset.UtcNow;
                var outcomes = await EvaluateAsync(interpreter, selection.Cases, apiRunLedger);
                var completedAt = DateTimeOffset.UtcNow;
                var report = BuildReport(
                    arguments,
                    selection,
                    outcomes,
                    model,
                    naturalLanguageOptions,
                    startedAt,
                    completedAt,
                    providerRunConfiguration,
                    apiRunLedger?.Snapshot());

                var reportPrefix = arguments.DatasetVersion == "v1"
                    ? arguments.Split
                    : $"{arguments.DatasetVersion}-{arguments.Mode}-{arguments.Split}";
                var outputPath = Path.GetFullPath(arguments.OutputPath ??
                    Path.Combine(
                        root,
                        "artifacts",
                        "evaluation",
                        "pm-analytics-interpretation",
                        $"{reportPrefix}-{startedAt:yyyyMMdd'T'HHmmss'Z'}.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await File.WriteAllTextAsync(
                    outputPath,
                    JsonSerializer.Serialize(report, JsonOptions),
                    new UTF8Encoding(false));

                Console.WriteLine(
                    $"Report written. Split={arguments.Split}; Mode={arguments.Mode}; " +
                    $"Cases={selection.Cases.Count}; ModelResponses={report.Execution.SuccessfulModelResponses}; " +
                    $"ProviderErrors={report.Execution.ProviderErrors}; EvaluatorErrors={report.Execution.EvaluatorErrors}.");
                return 0;
            }
            finally
            {
                client?.Dispose();
                apiModelClient?.Dispose();
            }
        }
        catch (GitSourceShaVerificationException exception)
        {
            Console.Error.WriteLine($"Evaluation setup failed: {exception.Code}");
            return 2;
        }
        catch (EvaluationSetupException exception)
        {
            Console.Error.WriteLine($"Evaluation setup failed: {exception.Code}");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Evaluation setup failed: {exception.GetType().Name}");
            return 2;
        }
    }

    private static async Task<IReadOnlyList<CaseOutcome>> EvaluateAsync(
        INaturalLanguageAnalyticsInterpreter interpreter,
        IReadOnlyList<EvaluationCase> cases,
        NaturalLanguageAnalyticsModelClientRunLedger? apiRunLedger = null)
    {
        var outcomes = new List<CaseOutcome>(cases.Count);
        foreach (var item in cases)
        {
            if (apiRunLedger?.Snapshot().TerminalFailureCode is not null)
            {
                outcomes.Add(CaseOutcome.NotExecutedCase(item));
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await interpreter.InterpretAsync(item.Question, CancellationToken.None);
                stopwatch.Stop();
                outcomes.Add(ScoreCase(item, result, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (NaturalLanguageAnalyticsProviderException exception)
            {
                stopwatch.Stop();
                outcomes.Add(CaseOutcome.Failure(
                    item,
                    providerError: exception.Failure.ToString(),
                    evaluatorError: null,
                    stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                outcomes.Add(CaseOutcome.Failure(
                    item,
                    providerError: null,
                    evaluatorError: exception.GetType().Name,
                    stopwatch.Elapsed.TotalMilliseconds));
            }
        }

        return outcomes;
    }

    internal static CaseOutcome ScoreCase(
        EvaluationCase item,
        PmAnalyticsInterpretationResult result,
        double latencyMilliseconds)
    {
        var status = result.Status.ToString();
        SafePlan? actualPlan = null;
        var actualPlanInvalid = false;
        if (result.Plan is not null)
        {
            if (PmAnalyticsPlanValidator.TryNormalize(result.Plan, out var normalized, out _))
            {
                actualPlan = ToSafePlan(normalized);
            }
            else
            {
                actualPlanInvalid = true;
            }
        }

        var fields = result.ClarificationFields.Select(field => field.ToString()).ToArray();
        var expected = item.Expected;
        var statusCorrect = status == expected.Status;
        var expectedValid = expected.Status == "Valid";
        var completePlanCorrect = expectedValid
            && result.Status == PmAnalyticsInterpretationStatus.Valid
            && expected.Plan is not null
            && actualPlan is not null
            && PlansEqual(expected.Plan, actualPlan);
        var clarificationExact = expected.Status == "NeedsClarification"
            && result.Status == PmAnalyticsInterpretationStatus.NeedsClarification
            && new HashSet<string>(expected.ClarificationFields, StringComparer.Ordinal)
                .SetEquals(fields);
        var unsupportedRejected = expected.Status == "Unsupported"
            && result.Status == PmAnalyticsInterpretationStatus.Unsupported;
        var validFields = expectedValid && expected.Plan is not null
            ? new FieldScores(
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Plan.Metric == actualPlan?.Metric,
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Plan.AssetCategory == actualPlan?.AssetCategory,
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Plan.PmCycle == actualPlan?.PmCycle,
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Plan.Department == actualPlan?.Department,
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Plan.GroupBy == actualPlan?.GroupBy,
                result.Status == PmAnalyticsInterpretationStatus.Valid
                    && expected.Presentation == result.Presentation?.ToString())
            : null;

        return new CaseOutcome(
            item.CaseId,
            item.Split,
            item.Language,
            item.CaseClass,
            item.CaseTag,
            expected.Status,
            expected.Plan is null ? null : ToSafePlan(expected.Plan),
            status,
            actualPlan,
            actualPlanInvalid,
            fields,
            result.Presentation?.ToString(),
            SafeInterpretationCode(result.Code),
            null,
            null,
            result.Usage,
            Math.Round(latencyMilliseconds, 2),
            statusCorrect,
            expectedValid ? completePlanCorrect : null,
            expectedValid ? validFields : null,
            expected.Status == "NeedsClarification" ? clarificationExact : null,
            expected.Status == "Unsupported" ? unsupportedRejected : null,
            expected.Status != "Valid" && (result.Plan is not null || result.Presentation is not null));
    }

    internal static EvaluationReport BuildReport(
        EvaluationArguments arguments,
        DatasetSelection selection,
        IReadOnlyList<CaseOutcome> outcomes,
        ModelMetadata? model,
        NaturalLanguageAnalyticsOptions configuration,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        ProviderRunConfiguration? providerRunConfiguration = null,
        NaturalLanguageAnalyticsModelClientRunSnapshot? apiRun = null)
    {
        var cases = selection.Cases;
        var executed = outcomes.Where(item => !item.NotExecuted).ToArray();
        var valid = executed.Where(item => item.ExpectedStatus == "Valid").ToArray();
        var clarifications = executed.Where(item => item.ExpectedStatus == "NeedsClarification").ToArray();
        var unsupported = executed.Where(item => item.ExpectedStatus == "Unsupported").ToArray();
        var strictControls = executed.Where(item => item.CaseTag == "strict-template-control").ToArray();
        var freePhrasing = valid.Where(item => item.CaseTag != "strict-template-control").ToArray();

        var validFieldScores = new Dictionary<string, MetricScore>(StringComparer.Ordinal)
        {
            ["metric"] = Score(outcomes.Count(item => item.FieldCorrectness?.Metric == true), valid.Length),
            ["assetCategory"] = Score(outcomes.Count(item => item.FieldCorrectness?.AssetCategory == true), valid.Length),
            ["pmCycle"] = Score(outcomes.Count(item => item.FieldCorrectness?.PmCycle == true), valid.Length),
            ["department"] = Score(outcomes.Count(item => item.FieldCorrectness?.Department == true), valid.Length),
            ["groupBy"] = Score(outcomes.Count(item => item.FieldCorrectness?.GroupBy == true), valid.Length),
            ["presentation"] = Score(outcomes.Count(item => item.FieldCorrectness?.Presentation == true), valid.Length)
        };

        var modelResponses = outcomes.Where(item => item.ProviderUsage is not null).ToArray();
        var apiUsage = apiRun is null
            ? null
            : BuildApiUsageAccounting(providerRunConfiguration!.Provider, apiRun);
        var usageTotals = apiUsage?.Totals ?? BuildCaseUsageTotals(modelResponses);
        var latency = executed.Select(item => item.LatencyMilliseconds).Order().ToArray();
        var providerErrors = outcomes.Count(item => item.ProviderError is not null);
        var guardedCases = outcomes.Count(item =>
            item.ProviderUsage is null && item.ProviderError is null && item.EvaluatorError is null);

        var modelConfiguration = arguments.Mode == "ollama"
            ? new ModelConfiguration(
                model?.Model,
                model?.Digest,
                model?.RuntimeVersion,
                OllamaNaturalLanguageAnalyticsInterpreter.PromptVersion,
                OllamaNaturalLanguageAnalyticsInterpreter.PromptFingerprint,
                OllamaNaturalLanguageAnalyticsInterpreter.GenerationTemperature,
                OllamaNaturalLanguageAnalyticsInterpreter.GenerationSeed,
                OllamaNaturalLanguageAnalyticsInterpreter.ContextTokens,
                configuration.TimeoutSeconds,
                configuration.MaxOutputTokens,
                configuration.MaxResponseBytes)
            : providerRunConfiguration is not null
                ? new ModelConfiguration(
                    providerRunConfiguration.Model,
                    null,
                    null,
                    ModelNaturalLanguageAnalyticsInterpreter.PromptVersion,
                    ModelNaturalLanguageAnalyticsInterpreter.PromptFingerprint,
                    providerRunConfiguration.Temperature,
                    null,
                    null,
                    NaturalLanguageAnalyticsApiModelClientBase.TimeoutSeconds,
                    NaturalLanguageAnalyticsApiModelClientBase.MaximumOutputTokens,
                    NaturalLanguageAnalyticsApiModelClientBase.MaximumResponseBytes)
            : null;

        return new EvaluationReport(
            arguments.DatasetVersion == "v2" ? 2 : 1,
            arguments.Split,
            arguments.Mode,
            arguments.SourceSha,
            selection.Sha256,
            startedAt,
            completedAt,
            modelConfiguration,
            new ExecutionCounts(
                cases.Count,
                outcomes.Count(item => item.EvaluatorError is null && !item.NotExecuted),
                apiRun?.Attempts ?? modelResponses.Length + providerErrors,
                apiRun?.ResponsesWithText ?? modelResponses.Length,
                providerErrors,
                outcomes.Count(item => item.EvaluatorError is not null),
                arguments.Mode == "ollama" ? guardedCases : null,
                arguments.Mode == "rule-based" ? guardedCases : null),
            new OverallScores(
                Score(executed.Count(item => item.StatusCorrect), executed.Length),
                Score(outcomes.Count(item => item.CompletePlanCorrect == true), valid.Length),
                validFieldScores,
                Score(outcomes.Count(item => item.ClarificationFieldsExact == true), clarifications.Length),
                Score(outcomes.Count(item => item.UnsupportedRejected == true), unsupported.Length),
                outcomes.Count(item => item.NonValidExecutableOutput)),
            new InputStyleScores(
                Score(strictControls.Count(item => item.StatusCorrect), strictControls.Length),
                Score(strictControls.Count(item => item.CompletePlanCorrect == true), strictControls.Length),
                Score(freePhrasing.Count(item => item.StatusCorrect), freePhrasing.Length),
                Score(freePhrasing.Count(item => item.CompletePlanCorrect == true), freePhrasing.Length)),
            BuildGroups(executed, item => item.Language),
            BuildGroups(executed, item => item.CaseClass),
            BuildGroups(valid, item =>
                item.CaseTag == "strict-template-control" ? "strict-template-control" : "free-phrasing"),
            BuildConfusion(outcomes),
            BuildProviderErrors(outcomes),
            new LatencyScores(
                latency.Length,
                Percentile(latency, 0.50),
                Percentile(latency, 0.95)),
            usageTotals,
            outcomes)
        {
            V2 = arguments.DatasetVersion == "v2"
                ? BuildV2ReportMetadata(
                    cases,
                    outcomes,
                    providerRunConfiguration,
                    apiRun,
                    apiUsage)
                : null
        };
    }

    private static UsageTotals BuildCaseUsageTotals(IReadOnlyList<CaseOutcome> modelResponses)
    {
        var promptTokens = modelResponses.Where(item => item.ProviderUsage!.PromptTokens.HasValue)
            .Select(item => item.ProviderUsage!.PromptTokens!.Value).ToArray();
        var completionTokens = modelResponses.Where(item => item.ProviderUsage!.CompletionTokens.HasValue)
            .Select(item => item.ProviderUsage!.CompletionTokens!.Value).ToArray();
        var providerDurations = modelResponses.Where(item => item.ProviderUsage!.DurationNanoseconds.HasValue)
            .Select(item => item.ProviderUsage!.DurationNanoseconds!.Value).ToArray();
        return new UsageTotals(
            SumIfComplete(promptTokens, modelResponses.Count),
            promptTokens.Length,
            SumIfComplete(completionTokens, modelResponses.Count),
            completionTokens.Length,
            SumIfComplete(providerDurations, modelResponses.Count),
            providerDurations.Length,
            null);
    }

    internal static ProviderUsageAccounting BuildApiUsageAccounting(
        string provider,
        NaturalLanguageAnalyticsModelClientRunSnapshot run)
    {
        var calls = run.Calls;
        var promptValues = calls.Where(call => call.Usage?.PromptTokens is >= 0)
            .Select(call => call.Usage!.PromptTokens!.Value).ToArray();
        var completionValues = calls.Where(call => call.Usage?.CompletionTokens is >= 0)
            .Select(call => call.Usage!.CompletionTokens!.Value).ToArray();
        var durationValues = calls.Where(call => call.Usage?.DurationNanoseconds is >= 0)
            .Select(call => call.Usage!.DurationNanoseconds!.Value).ToArray();
        var cachedValues = calls.Where(call => call.Usage?.CachedPromptTokens is >= 0)
            .Select(call => call.Usage!.CachedPromptTokens!.Value).ToArray();
        var cacheMissValues = calls.Where(call => call.Usage?.CacheMissPromptTokens is >= 0)
            .Select(call => call.Usage!.CacheMissPromptTokens!.Value).ToArray();
        var reasoningValues = calls.Where(call => call.Usage?.ReasoningTokens is >= 0)
            .Select(call => call.Usage!.ReasoningTokens!.Value).ToArray();

        var promptComplete = CompleteValues(calls, run.Attempts, usage => usage?.PromptTokens);
        var completionComplete = CompleteValues(calls, run.Attempts, usage => usage?.CompletionTokens);
        var durationComplete = CompleteValues(calls, run.Attempts, usage => usage?.DurationNanoseconds);
        var cost = CalculateProviderCost(provider, calls, run.Attempts);
        return new ProviderUsageAccounting(
            new UsageTotals(
                promptComplete ? SafeSum(promptValues) : null,
                promptValues.Length,
                completionComplete ? SafeSum(completionValues) : null,
                completionValues.Length,
                durationComplete ? SafeSum(durationValues) : null,
                durationValues.Length,
                cost.EstimatedCostUsd),
            CompleteValues(calls, run.Attempts, usage => usage?.CachedPromptTokens)
                ? SafeSum(cachedValues)
                : null,
            cachedValues.Length,
            CompleteValues(calls, run.Attempts, usage => usage?.CacheMissPromptTokens)
                ? SafeSum(cacheMissValues)
                : null,
            cacheMissValues.Length,
            CompleteValues(calls, run.Attempts, usage => usage?.ReasoningTokens)
                ? SafeSum(reasoningValues)
                : null,
            reasoningValues.Length,
            promptComplete && completionComplete,
            cost.Status,
            cost.Basis,
            cost.EstimatedCostUsd);
    }

    private static bool CompleteValues(
        IReadOnlyList<NaturalLanguageAnalyticsModelCallObservation> calls,
        int attempts,
        Func<NaturalLanguageAnalyticsProviderUsage?, long?> select)
        => attempts > 0
            && calls.Count == attempts
            && calls.All(call => call.HttpSucceeded
                && select(call.Usage) is long value
                && value >= 0);

    private static long? SafeSum(IReadOnlyList<long> values)
    {
        long sum = 0;
        foreach (var value in values)
        {
            if (value < 0 || long.MaxValue - sum < value)
            {
                return null;
            }

            sum += value;
        }

        return sum;
    }

    internal static ProviderCostEstimate CalculateProviderCost(
        string provider,
        IReadOnlyList<NaturalLanguageAnalyticsModelCallObservation> calls,
        int attempts)
    {
        if (attempts <= 0 || calls.Count != attempts
            || calls.Any(call => !call.HttpSucceeded
                || call.Usage?.PromptTokens is not >= 0
                || call.Usage?.CompletionTokens is not >= 0))
        {
            return new ProviderCostEstimate(null, "incomplete", "Usage is incomplete; estimated cost is unavailable.");
        }

        decimal total = 0;
        foreach (var call in calls)
        {
            var usage = call.Usage!;
            var prompt = usage.PromptTokens!.Value;
            var completion = usage.CompletionTokens!.Value;
            if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
            {
                total += prompt * (0.75m / 1_000_000m);
                total += completion * (3.75m / 1_000_000m);
            }
            else if (provider.Equals("deepseek", StringComparison.OrdinalIgnoreCase))
            {
                var cached = usage.CachedPromptTokens;
                var cacheMiss = usage.CacheMissPromptTokens;
                if (cached is >= 0 && cacheMiss is >= 0
                    && cached.Value <= prompt
                    && cacheMiss.Value == prompt - cached.Value)
                {
                    total += cached.Value * (0.006m / 1_000_000m);
                    total += cacheMiss.Value * (0.30m / 1_000_000m);
                }
                else
                {
                    total += prompt * (0.30m / 1_000_000m);
                }

                total += completion * (1.20m / 1_000_000m);
            }
            else
            {
                return new ProviderCostEstimate(null, "unavailable", "No cost profile is defined for this provider.");
            }
        }

        var basis = provider.Equals("gemini", StringComparison.OrdinalIgnoreCase)
            ? "Pricing profile checked 2026-10-04; standard full-input rate upper bound through 2026-12-31. Billed completion tokens already include reasoning; no cache discount applied."
            : "Pricing profile checked 2026-10-04; peak upper bound at $0.30/M cache-miss input, $0.006/M cache-hit input, and $1.20/M output. When cache attribution is unavailable, all input is priced as cache misses.";
        return new ProviderCostEstimate(
            Math.Round(total, 8, MidpointRounding.AwayFromZero),
            "complete",
            basis);
    }

    private static V2ReportMetadata BuildV2ReportMetadata(
        IReadOnlyList<EvaluationCase> cases,
        IReadOnlyList<CaseOutcome> outcomes,
        ProviderRunConfiguration? configuration,
        NaturalLanguageAnalyticsModelClientRunSnapshot? apiRun,
        ProviderUsageAccounting? usage)
    {
        var executed = outcomes.Where(item => !item.NotExecuted).ToArray();
        var byLanguage = executed
            .GroupBy(item => item.Language, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildScoreBreakdown(group),
                StringComparer.Ordinal);
        var byInputStyle = executed
            .Where(item => item.ExpectedStatus == "Valid")
            .GroupBy(item => item.CaseTag == "strict-template-control"
                ? "strict-template-control"
                : "free-phrasing", StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildScoreBreakdown(group),
                StringComparer.Ordinal);
        var byExpectedStatus = executed
            .GroupBy(item => item.ExpectedStatus, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildScoreBreakdown(group),
                StringComparer.Ordinal);
        var byExpectedMetric = executed
            .Where(item => item.ExpectedPlan is not null)
            .GroupBy(item => item.ExpectedPlan!.Metric, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MetricBreakdown(
                group.Key,
                BuildScoreBreakdown(group)))
            .ToArray();
        var adversarial = executed.Where(item => item.CaseClass == "adversarial").ToArray();
        ProviderRunSummary? providerRun = null;
        if (configuration is not null && apiRun is not null && usage is not null)
        {
            var latencies = apiRun.Calls.Select(call => call.LatencyMilliseconds).Order().ToArray();
            var runEvaluationComplete = outcomes.Count == cases.Count
                && outcomes.All(item => !item.NotExecuted && item.EvaluatorError is null)
                && apiRun.TerminalFailureCode is null;
            providerRun = new ProviderRunSummary(
                configuration.Provider,
                configuration.Model,
                configuration.ApiVersion,
                ModelNaturalLanguageAnalyticsInterpreter.PromptVersion,
                ModelNaturalLanguageAnalyticsInterpreter.PromptFingerprint,
                1,
                configuration.Temperature,
                configuration.ReasoningSetting,
                NaturalLanguageAnalyticsApiModelClientBase.TimeoutSeconds,
                NaturalLanguageAnalyticsApiModelClientBase.MaximumOutputTokens,
                NaturalLanguageAnalyticsApiModelClientBase.MaximumResponseBytes,
                NaturalLanguageAnalyticsModelClientRunLedger.MaximumRequests,
                apiRun.Attempts,
                outcomes.Count(item => item.NotExecuted),
                apiRun.HttpSuccessResponses,
                apiRun.ResponsesWithText,
                apiRun.ProviderFailures,
                apiRun.TerminalFailureCode,
                runEvaluationComplete,
                usage.BaseUsageComplete,
                usage.CachedPromptTokens,
                usage.CachedPromptTokenResponses,
                usage.CacheMissPromptTokens,
                usage.CacheMissPromptTokenResponses,
                usage.ReasoningTokens,
                usage.ReasoningTokenResponses,
                usage.CostStatus,
                usage.CostBasis,
                new LatencyScores(latencies.Length, Percentile(latencies, 0.50), Percentile(latencies, 0.95)),
                apiRun.Calls.Select((call, index) => new ProviderCallSummary(
                    index + 1,
                    call.HttpSucceeded,
                    call.HasText,
                    call.LatencyMilliseconds,
                    call.ReportedModelVersion,
                    call.SystemFingerprint,
                    call.SafeErrorCode,
                    call.Usage)).ToArray());
        }

        return new V2ReportMetadata(
            "v2",
            "provisional",
            false,
            false,
            outcomes.Count == cases.Count
                && outcomes.All(item => !item.NotExecuted && item.EvaluatorError is null)
                && apiRun?.TerminalFailureCode is null,
            60,
            30,
            20,
            10,
            cases.Count,
            executed.Length,
            outcomes.Count(item => item.NotExecuted),
            byLanguage,
            byInputStyle,
            byExpectedStatus,
            byExpectedMetric,
            Score(adversarial.Count(item => item.UnsupportedRejected == true), adversarial.Length),
            providerRun);
    }

    private static CaseScoreBreakdown BuildScoreBreakdown(IEnumerable<CaseOutcome> selected)
    {
        var outcomes = selected.Where(item => !item.NotExecuted).ToArray();
        var valid = outcomes.Where(item => item.ExpectedStatus == "Valid").ToArray();
        var clarifications = outcomes.Where(item => item.ExpectedStatus == "NeedsClarification").ToArray();
        var unsupported = outcomes.Where(item => item.ExpectedStatus == "Unsupported").ToArray();
        var adversarial = outcomes.Where(item => item.CaseClass == "adversarial").ToArray();
        var fields = new Dictionary<string, MetricScore>(StringComparer.Ordinal)
        {
            ["metric"] = Score(valid.Count(item => item.FieldCorrectness?.Metric == true), valid.Length),
            ["assetCategory"] = Score(valid.Count(item => item.FieldCorrectness?.AssetCategory == true), valid.Length),
            ["pmCycle"] = Score(valid.Count(item => item.FieldCorrectness?.PmCycle == true), valid.Length),
            ["department"] = Score(valid.Count(item => item.FieldCorrectness?.Department == true), valid.Length),
            ["groupBy"] = Score(valid.Count(item => item.FieldCorrectness?.GroupBy == true), valid.Length),
            ["presentation"] = Score(valid.Count(item => item.FieldCorrectness?.Presentation == true), valid.Length)
        };
        return new CaseScoreBreakdown(
            outcomes.Length,
            Score(outcomes.Count(item => item.StatusCorrect), outcomes.Length),
            Score(valid.Count(item => item.CompletePlanCorrect == true), valid.Length),
            fields,
            Score(clarifications.Count(item => item.ClarificationFieldsExact == true), clarifications.Length),
            Score(unsupported.Count(item => item.UnsupportedRejected == true), unsupported.Length),
            Score(adversarial.Count(item => item.UnsupportedRejected == true), adversarial.Length),
            outcomes.Count(item => item.NonValidExecutableOutput));
    }

    private static IReadOnlyList<GroupScores> BuildGroups(
        IEnumerable<CaseOutcome> outcomes,
        Func<CaseOutcome, string> key)
        => outcomes.GroupBy(key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new GroupScores(
                group.Key,
                group.Count(),
                group.Count(item => item.ProviderError is null
                    && item.EvaluatorError is null
                    && !item.NotExecuted),
                group.Count(item => item.ProviderError is not null),
                group.Count(item => item.EvaluatorError is not null),
                Score(group.Count(item => item.StatusCorrect), group.Count()))
            ).ToArray();

    private static IReadOnlyDictionary<string, int> BuildConfusion(IEnumerable<CaseOutcome> outcomes)
        => outcomes.Where(item => item.ActualStatus is not null)
            .GroupBy(item => $"{item.ExpectedStatus} -> {item.ActualStatus}", StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> BuildProviderErrors(IEnumerable<CaseOutcome> outcomes)
        => outcomes.Where(item => item.ProviderError is not null)
            .GroupBy(item => item.ProviderError!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static long? SumIfComplete(long[] values, int modelResponses)
        => modelResponses == 0 || values.Length != modelResponses ? null : values.Sum();

    private static SafePlan ToSafePlan(ExpectedPlan plan)
        => new(plan.Metric, plan.AssetCategory, plan.PmCycle, plan.Department, plan.GroupBy);

    private static SafePlan ToSafePlan(PmAnalyticsPlan plan)
        => new(plan.Metric.ToString(), plan.AssetCategory, plan.PmCycle, plan.Department, plan.GroupBy.ToString());

    private static bool PlansEqual(ExpectedPlan expected, SafePlan actual)
        => expected.Metric == actual.Metric
            && expected.AssetCategory == actual.AssetCategory
            && expected.PmCycle == actual.PmCycle
            && expected.Department == actual.Department
            && expected.GroupBy == actual.GroupBy;

    private static MetricScore Score(int correct, int denominator)
        => new(correct, denominator, denominator == 0 ? null : Math.Round((decimal)correct / denominator, 4));

    private static double? Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var index = Math.Clamp((int)Math.Ceiling(percentile * values.Count) - 1, 0, values.Count - 1);
        return Math.Round(values[index], 2);
    }

    private static string? SafeInterpretationCode(string? code)
        => code is null
            ? null
            : code is "ComparisonNotSupported"
                or "GroupingNotSupported"
                or "RequestNotSupported"
                or "UnsafeRequestNotSupported"
                or "QuestionNotSupported"
                or "UnsupportedPmCycle"
                or "UnsafeDepartment"
                or "YearRequired"
                or "MonthRequired"
                ? code
                : "Other";

    internal static async Task<DatasetSelection> ReadCasesAsync(
        string datasetPath,
        string split,
        string datasetVersion = "v1")
    {
        ValidateDatasetExecutionAllowed(datasetVersion, split);
        if (!File.Exists(datasetPath))
        {
            throw new EvaluationSetupException("DatasetNotFound");
        }

        var bytes = await File.ReadAllBytesAsync(datasetPath);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifestPath = Path.Combine(Path.GetDirectoryName(datasetPath)!, ManifestFileName);
        if (datasetVersion == "v2")
        {
            EnsureV2Manifest(manifestPath, sha256);
        }
        else
        {
            EnsureManifestHash(manifestPath, sha256);
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new EvaluationSetupException("DatasetEncodingInvalid");
        }

        var allCases = new List<EvaluationCase>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var familySplits = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            EvaluationCase? item;
            try
            {
                item = JsonSerializer.Deserialize<EvaluationCase>(line, JsonOptions);
            }
            catch (JsonException)
            {
                throw new EvaluationSetupException($"DatasetJsonInvalidAtLine{lineNumber}");
            }

            if (item is null
                || string.IsNullOrWhiteSpace(item.CaseId)
                || string.IsNullOrWhiteSpace(item.FamilyId)
                || string.IsNullOrWhiteSpace(item.Question)
                || item.Expected is null
                || item.Split is not ("dev" or "heldout")
                || item.Language is not ("en" or "fil" or "taglish")
                || item.CaseClass is not ("valid" or "needs-clarification" or "unsupported" or "adversarial")
                || !ids.Add(item.CaseId))
            {
                throw new EvaluationSetupException($"DatasetCaseInvalidAtLine{lineNumber}");
            }

            RecordFamilySplit(item.FamilyId, item.Split, familySplits);
            ValidateExpected(item, lineNumber);
            allCases.Add(item);
        }

        if (datasetVersion == "v2")
        {
            ValidateV2Distribution(allCases);
        }
        else
        {
            ValidateV1Distribution(allCases, split);
        }

        var selected = allCases.Where(item => item.Split == split).ToArray();
        return new DatasetSelection(selected, sha256);
    }

    internal static void ValidateDatasetExecutionAllowed(string datasetVersion, string split)
    {
        if (datasetVersion is not ("v1" or "v2") || split is not ("dev" or "heldout"))
        {
            throw new EvaluationSetupException("InvalidDatasetSelection");
        }

        if (datasetVersion == "v2" && split == "heldout")
        {
            throw new EvaluationSetupException("V2HeldoutNotAuthorized");
        }
    }

    internal static void RecordFamilySplit(
        string familyId,
        string split,
        IDictionary<string, string> familySplits)
    {
        if (familySplits.TryGetValue(familyId, out var familySplit) && familySplit != split)
        {
            throw new EvaluationSetupException("FamilyCrossesSplits");
        }

        familySplits[familyId] = split;
    }

    private static void ValidateV1Distribution(IReadOnlyList<EvaluationCase> cases, string split)
    {
        if (cases.Count != 100)
        {
            throw new EvaluationSetupException("UnexpectedDatasetCount");
        }

        var selected = cases.Where(item => item.Split == split).ToArray();
        var expectedCount = split == "dev" ? 60 : 40;
        var expectedPerClass = split == "dev" ? 15 : 10;
        var classCounts = selected.GroupBy(item => item.CaseClass, StringComparer.Ordinal)
            .Select(group => group.Count()).ToArray();
        var controlCount = selected.Count(item => item.CaseTag == "strict-template-control");
        var expectedControlCount = split == "dev" ? 3 : 2;
        if (selected.Length != expectedCount
            || classCounts.Length != 4
            || classCounts.Any(count => count != expectedPerClass)
            || controlCount != expectedControlCount)
        {
            throw new EvaluationSetupException("UnexpectedSplitDistribution");
        }
    }

    internal static void ValidateV2Distribution(IReadOnlyList<EvaluationCase> cases)
    {
        if (cases.Count != 90)
        {
            throw new EvaluationSetupException("UnexpectedV2DatasetCount");
        }

        var families = cases.GroupBy(item => item.FamilyId, StringComparer.Ordinal).ToArray();
        if (families.Length != 30
            || families.Any(family => family.Count() != 3
                || family.Select(item => item.Language).Distinct(StringComparer.Ordinal).Count() != 3
                || family.Any(item => item.Split != family.First().Split)))
        {
            throw new EvaluationSetupException("UnexpectedV2FamilyDistribution");
        }

        var familySplitCounts = families.GroupBy(family => family.First().Split, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (familySplitCounts.GetValueOrDefault("dev") != 20
            || familySplitCounts.GetValueOrDefault("heldout") != 10)
        {
            throw new EvaluationSetupException("UnexpectedV2FamilySplitDistribution");
        }

        foreach (var split in new[] { "dev", "heldout" })
        {
            var selected = cases.Where(item => item.Split == split).ToArray();
            var expectedCases = split == "dev" ? 60 : 30;
            var expectedClasses = split == "dev"
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["valid"] = 15,
                    ["needs-clarification"] = 15,
                    ["unsupported"] = 15,
                    ["adversarial"] = 15
                }
                : new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["valid"] = 6,
                    ["needs-clarification"] = 15,
                    ["unsupported"] = 6,
                    ["adversarial"] = 3
                };
            var classCounts = selected.GroupBy(item => item.CaseClass, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var controlCount = selected.Count(item => item.CaseTag == "strict-template-control");
            var expectedControls = split == "dev" ? 3 : 1;
            if (selected.Length != expectedCases
                || classCounts.Count != expectedClasses.Count
                || expectedClasses.Any(pair => classCounts.GetValueOrDefault(pair.Key) != pair.Value)
                || controlCount != expectedControls)
            {
                throw new EvaluationSetupException("UnexpectedV2SplitDistribution");
            }

            var expectedStatuses = split == "dev"
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Valid"] = 15,
                    ["NeedsClarification"] = 15,
                    ["Unsupported"] = 30
                }
                : new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Valid"] = 6,
                    ["NeedsClarification"] = 15,
                    ["Unsupported"] = 9
                };
            var statusCounts = selected.GroupBy(item => item.Expected.Status, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (expectedStatuses.Any(pair => statusCounts.GetValueOrDefault(pair.Key) != pair.Value)
                || statusCounts.Count != expectedStatuses.Count)
            {
                throw new EvaluationSetupException("UnexpectedV2StatusDistribution");
            }

            foreach (var language in new[] { "en", "fil", "taglish" })
            {
                if (selected.Count(item => item.Language == language) != expectedCases / 3)
                {
                    throw new EvaluationSetupException("UnexpectedV2LanguageDistribution");
                }
            }
        }

        var developmentValid = cases.Where(item => item.Split == "dev" && item.Expected.Status == "Valid").ToArray();
        var expectedMetrics = new[] { "Progress", "OnTimeCompliance", "CompletedLate", "NonOperational" };
        var expectedCategories = new[]
        {
            "fire-extinguisher", "fire-alarm", "emergency-light", "water-drinking-station"
        };
        if (!expectedMetrics.All(metric => developmentValid.Any(item => item.Expected.Plan?.Metric == metric))
            || !expectedCategories.All(category => developmentValid.Any(item => item.Expected.Plan?.AssetCategory == category)))
        {
            throw new EvaluationSetupException("MissingV2DevelopmentPlanCoverage");
        }
    }

    private static void ValidateExpected(EvaluationCase item, int lineNumber)
    {
        if (item.Expected is null || item.Expected.ClarificationFields is null)
        {
            throw new EvaluationSetupException($"DatasetLabelInvalidAtLine{lineNumber}");
        }

        var validShape = item.Expected.Status switch
        {
            "Valid" => item.Expected.Plan is not null
                && IsCanonicalPlan(item.Expected.Plan)
                && IsPresentationValid(item.Expected.Plan, item.Expected.Presentation)
                && item.Expected.ClarificationFields.Length == 0,
            "NeedsClarification" => item.Expected.Plan is null
                && item.Expected.Presentation is null
                && item.Expected.ClarificationFields.Length > 0
                && IsKnownClarificationSet(item.Expected.ClarificationFields),
            "Unsupported" => item.Expected.Plan is null
                && item.Expected.Presentation is null
                && item.Expected.ClarificationFields.Length == 0,
            _ => false
        };
        var classMatches = item.CaseClass switch
        {
            "valid" => item.Expected.Status == "Valid",
            "needs-clarification" => item.Expected.Status == "NeedsClarification",
            "unsupported" or "adversarial" => item.Expected.Status == "Unsupported",
            _ => false
        };
        var tagMatches = item.CaseTag is null
            || item.CaseTag == "strict-template-control"
                && item.CaseClass == "valid"
                && item.Language == "en";
        if (!validShape || !classMatches || !tagMatches)
        {
            throw new EvaluationSetupException($"DatasetLabelInvalidAtLine{lineNumber}");
        }
    }

    private static bool IsCanonicalPlan(ExpectedPlan plan)
    {
        if (plan.Metric is null
            || plan.AssetCategory is null
            || plan.PmCycle is null
            || plan.GroupBy is null
            || !Enum.TryParse<PmAnalyticsMetric>(plan.Metric, false, out var metric)
            || !Enum.IsDefined(metric)
            || !Enum.TryParse<PmAnalyticsGroupBy>(plan.GroupBy, false, out var groupBy)
            || !Enum.IsDefined(groupBy))
        {
            return false;
        }

        var candidate = new PmAnalyticsPlan(
            metric,
            plan.AssetCategory,
            plan.PmCycle,
            plan.Department,
            groupBy);
        if (!PmAnalyticsPlanValidator.TryNormalize(candidate, out var normalized, out _))
        {
            return false;
        }

        return plan.Metric == normalized.Metric.ToString()
            && plan.AssetCategory == normalized.AssetCategory
            && plan.PmCycle == normalized.PmCycle
            && plan.Department == normalized.Department
            && plan.GroupBy == normalized.GroupBy.ToString();
    }

    private static bool IsPresentationValid(ExpectedPlan plan, string? presentation)
    {
        if (presentation is not ("Count" or "Percent"))
        {
            return false;
        }

        return plan.Metric switch
        {
            "Progress" => true,
            "OnTimeCompliance" => presentation == "Percent",
            "CompletedLate" or "NonOperational" => presentation == "Count",
            _ => false
        };
    }

    private static bool IsKnownClarificationSet(string[] fields)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Metric", "AssetCategory", "Year", "Month", "Department", "GroupBy"
        };
        return fields.All(field => !string.IsNullOrWhiteSpace(field) && allowed.Contains(field))
            && fields.Distinct(StringComparer.Ordinal).Count() == fields.Length;
    }

    private static void EnsureManifestHash(string manifestPath, string sha256)
    {
        if (!File.Exists(manifestPath))
        {
            throw new EvaluationSetupException("SplitManifestNotFound");
        }

        var manifest = File.ReadAllText(manifestPath).Replace('\u0060', ' ');
        var match = Regex.Match(
            manifest,
            @"Cases SHA-256 \(UTF-8, no BOM, LF line endings\):\s*(?<digest>[0-9a-f]{64})",
            RegexOptions.CultureInvariant);
        if (!match.Success || match.Groups["digest"].Value != sha256)
        {
            throw new EvaluationSetupException("DatasetDigestMismatch");
        }
    }

    private static void EnsureV2Manifest(string manifestPath, string sha256)
    {
        if (!File.Exists(manifestPath))
        {
            throw new EvaluationSetupException("SplitManifestNotFound");
        }

        var manifest = File.ReadAllText(manifestPath);
        if (!Regex.IsMatch(
                manifest,
                @"\A---\s*\r?\nid:\s*NLA-INTERPRETATION-V2-SPLIT\s*\r?\nstatus:\s*provisional\s*\r?\n---",
                RegexOptions.CultureInvariant))
        {
            throw new EvaluationSetupException("V2ManifestNotProvisional");
        }

        var match = Regex.Match(
            manifest,
            @"Current cases SHA-256 \(UTF-8 without BOM, LF line endings\):\s*(?<digest>[0-9a-f]{64})",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || match.Groups["digest"].Value != sha256
            || sha256 != V2DatasetSha256)
        {
            throw new EvaluationSetupException("DatasetDigestMismatch");
        }
    }

    private static async Task<ModelMetadata> ReadModelMetadataAsync(HttpClient client, string model)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var tagsResponse = await client.GetAsync("api/tags", timeout.Token);
            if (!tagsResponse.IsSuccessStatusCode)
            {
                throw new EvaluationSetupException("OllamaMetadataUnavailable");
            }

            await using var tagsStream = await tagsResponse.Content.ReadAsStreamAsync(timeout.Token);
            using var tags = await JsonDocument.ParseAsync(tagsStream, cancellationToken: timeout.Token);
            if (!tags.RootElement.TryGetProperty("models", out var models)
                || models.ValueKind != JsonValueKind.Array)
            {
                throw new EvaluationSetupException("OllamaModelListInvalid");
            }

            var entry = models.EnumerateArray().FirstOrDefault(item =>
                (item.TryGetProperty("name", out var name) && name.GetString() == model)
                || (item.TryGetProperty("model", out var modelName) && modelName.GetString() == model));
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("digest", out var digest)
                || digest.ValueKind != JsonValueKind.String)
            {
                throw new EvaluationSetupException("RequestedModelNotInstalled");
            }

            using var versionResponse = await client.GetAsync("api/version", timeout.Token);
            if (!versionResponse.IsSuccessStatusCode)
            {
                throw new EvaluationSetupException("OllamaVersionUnavailable");
            }

            await using var versionStream = await versionResponse.Content.ReadAsStreamAsync(timeout.Token);
            using var versionJson = await JsonDocument.ParseAsync(versionStream, cancellationToken: timeout.Token);
            if (!versionJson.RootElement.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.String)
            {
                throw new EvaluationSetupException("OllamaVersionInvalid");
            }

            return new ModelMetadata(model, digest.GetString()!, version.GetString()!);
        }
        catch (EvaluationSetupException)
        {
            throw;
        }
        catch
        {
            throw new EvaluationSetupException("OllamaMetadataUnavailable");
        }
    }

    private static Uri ValidateLoopbackAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp
            || !uri.IsLoopback
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || uri.AbsolutePath != "/"
            || uri.Port != 11434)
        {
            throw new EvaluationSetupException("OllamaMustUseLoopback11434");
        }

        return uri;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, DatasetRelativePath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new EvaluationSetupException("RepositoryRootNotFound");
    }

    internal static bool TryParseArguments(
        string[] args,
        out EvaluationArguments arguments,
        out string error)
    {
        arguments = new EvaluationArguments();
        error = string.Empty;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--help")
            {
                arguments.ShowHelp = true;
                continue;
            }

            if (!args[index].StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal)
                || !values.TryAdd(args[index], args[index + 1]))
            {
                error = "Invalid or duplicate command-line option.";
                return false;
            }

            index++;
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "--split", "--mode", "--source-sha", "--model", "--base-address", "--dataset", "--output",
            "--dataset-version"
        };
        if (values.Keys.Any(key => !allowed.Contains(key)))
        {
            error = "Unknown command-line option.";
            return false;
        }

        if (arguments.ShowHelp)
        {
            return true;
        }

        if (!values.TryGetValue("--split", out var split) || split is not ("dev" or "heldout"))
        {
            error = "Specify --split dev or --split heldout.";
            return false;
        }

        var datasetVersion = values.GetValueOrDefault("--dataset-version") ?? "v1";
        if (datasetVersion is not ("v1" or "v2"))
        {
            error = "Specify --dataset-version v1 or v2.";
            return false;
        }

        if (!values.TryGetValue("--mode", out var mode)
            || mode is not ("rule-based" or "ollama" or "gemini" or "deepseek"))
        {
            error = "Specify --mode rule-based, ollama, gemini, or deepseek.";
            return false;
        }

        if (mode is "gemini" or "deepseek")
        {
            if (datasetVersion != "v2")
            {
                error = "Gemini and DeepSeek modes require --dataset-version v2.";
                return false;
            }

            if (values.ContainsKey("--model") || values.ContainsKey("--base-address"))
            {
                error = "Cloud provider model and endpoint settings are fixed by the evaluator.";
                return false;
            }
        }
        else if (datasetVersion == "v2" && mode == "ollama")
        {
            error = "Ollama mode remains on dataset version v1.";
            return false;
        }

        if (!values.TryGetValue("--source-sha", out var sourceSha)
            || !Regex.IsMatch(sourceSha, @"\A[0-9a-f]{40}\z", RegexOptions.CultureInvariant))
        {
            error = "Specify the exact full source SHA with --source-sha.";
            return false;
        }

        arguments.Split = split;
        arguments.Mode = mode;
        arguments.DatasetVersion = datasetVersion;
        arguments.SourceSha = sourceSha;
        arguments.Model = values.GetValueOrDefault("--model") ?? "qwen3:4b-instruct";
        arguments.BaseAddress = values.GetValueOrDefault("--base-address") ?? "http://127.0.0.1:11434/";
        arguments.DatasetPath = values.GetValueOrDefault("--dataset");
        arguments.OutputPath = values.GetValueOrDefault("--output");
        return true;
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private const string Usage = """
        PM analytics interpretation evaluator
        Required: --split dev|heldout --mode rule-based|ollama|gemini|deepseek --source-sha <full-commit-sha>
        Optional: --dataset-version v1|v2 --model <local-model-tag> --base-address <loopback-ollama-url>
                  --dataset <cases.jsonl> --output <report.json>

        Dataset v2 is provisional and development-only. Gemini and DeepSeek read keys from GEMINI_API_KEY
        and DEEPSEEK_API_KEY respectively; held-out v2 evaluation is disabled.
        """;

    internal sealed class EvaluationArguments
    {
        public bool ShowHelp { get; set; }
        public string Split { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string DatasetVersion { get; set; } = "v1";
        public string SourceSha { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string BaseAddress { get; set; } = string.Empty;
        public string? DatasetPath { get; set; }
        public string? OutputPath { get; set; }
    }

    internal sealed record EvaluationCase(
        string CaseId,
        string FamilyId,
        string Split,
        string Language,
        string CaseClass,
        string Question,
        ExpectedLabel Expected,
        string? CaseTag);

    internal sealed record ExpectedLabel(
        string Status,
        ExpectedPlan? Plan,
        string[] ClarificationFields,
        string? Presentation);

    internal sealed record ExpectedPlan(
        string Metric,
        string AssetCategory,
        string PmCycle,
        string? Department,
        string GroupBy);

    internal sealed record SafePlan(
        string Metric,
        string AssetCategory,
        string PmCycle,
        string? Department,
        string GroupBy);

    internal sealed record FieldScores(
        bool Metric,
        bool AssetCategory,
        bool PmCycle,
        bool Department,
        bool GroupBy,
        bool Presentation);

    internal sealed record CaseOutcome(
        string CaseId,
        string Split,
        string Language,
        string CaseClass,
        string? CaseTag,
        string ExpectedStatus,
        SafePlan? ExpectedPlan,
        string? ActualStatus,
        SafePlan? ActualPlan,
        bool ActualPlanInvalid,
        string[] ActualClarificationFields,
        string? ActualPresentation,
        string? InterpretationCode,
        string? ProviderError,
        string? EvaluatorError,
        NaturalLanguageAnalyticsProviderUsage? ProviderUsage,
        double LatencyMilliseconds,
        bool StatusCorrect,
        bool? CompletePlanCorrect,
        FieldScores? FieldCorrectness,
        bool? ClarificationFieldsExact,
        bool? UnsupportedRejected,
        bool NonValidExecutableOutput,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        bool NotExecuted = false)
    {
        public static CaseOutcome Failure(
            EvaluationCase item,
            string? providerError,
            string? evaluatorError,
            double latencyMilliseconds)
            => new(
                item.CaseId,
                item.Split,
                item.Language,
                item.CaseClass,
                item.CaseTag,
                item.Expected.Status,
                item.Expected.Plan is null ? null : ToSafePlan(item.Expected.Plan),
                null,
                null,
                false,
                [],
                null,
                null,
                providerError,
                evaluatorError,
                null,
                Math.Round(latencyMilliseconds, 2),
                false,
                item.Expected.Status == "Valid" ? false : null,
                item.Expected.Status == "Valid"
                    ? new FieldScores(false, false, false, false, false, false)
                    : null,
                item.Expected.Status == "NeedsClarification" ? false : null,
                item.Expected.Status == "Unsupported" ? false : null,
                false);

        public static CaseOutcome NotExecutedCase(EvaluationCase item)
            => Failure(item, providerError: null, evaluatorError: null, 0) with { NotExecuted = true };
    }

    internal sealed record DatasetSelection(IReadOnlyList<EvaluationCase> Cases, string Sha256);

    internal sealed record MetricScore(int Correct, int Denominator, decimal? Rate);

    internal sealed record GroupScores(
        string Group,
        int Cases,
        int Interpreted,
        int ProviderErrors,
        int EvaluatorErrors,
        MetricScore StatusAccuracy);

    internal sealed record OverallScores(
        MetricScore StatusAccuracy,
        MetricScore CompletePlanAccuracyOnExpectedValid,
        IReadOnlyDictionary<string, MetricScore> ValidFieldAccuracy,
        MetricScore ExactClarificationFieldsOnExpectedClarification,
        MetricScore UnsupportedRejectionOnExpectedUnsupported,
        int NonValidExecutableOutputCount);

    internal sealed record InputStyleScores(
        MetricScore StrictTemplateControlStatusAccuracy,
        MetricScore StrictTemplateControlPlanAccuracy,
        MetricScore FreePhrasingValidStatusAccuracy,
        MetricScore FreePhrasingValidPlanAccuracy);

    internal sealed record LatencyScores(
        int SampleCount,
        double? P50Milliseconds,
        double? P95Milliseconds);

    internal sealed record UsageTotals(
        long? PromptTokens,
        int PromptTokenResponses,
        long? CompletionTokens,
        int CompletionTokenResponses,
        long? ProviderDurationNanoseconds,
        int ProviderDurationResponses,
        decimal? Cost);

    internal sealed record ModelMetadata(string Model, string Digest, string RuntimeVersion);

    internal sealed record ModelConfiguration(
        string? Model,
        string? ModelDigest,
        string? RuntimeVersion,
        string? PromptVersion,
        string? PromptFingerprint,
        double? Temperature,
        int? Seed,
        int? ContextTokens,
        int? TimeoutSeconds,
        int? MaxOutputTokens,
        int? MaxResponseBytes);

    internal sealed record ExecutionCounts(
        int Cases,
        int EvaluatedCases,
        int ProviderAttempts,
        int SuccessfulModelResponses,
        int ProviderErrors,
        int EvaluatorErrors,
        int? GuardedCases,
        int? RuleBasedCases);

    internal sealed record EvaluationReport(
        int SchemaVersion,
        string Split,
        string Mode,
        string SourceSha,
        string DatasetSha256,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc,
        ModelConfiguration? ModelConfiguration,
        ExecutionCounts Execution,
        OverallScores Scores,
        InputStyleScores InputStyles,
        IReadOnlyList<GroupScores> ByLanguage,
        IReadOnlyList<GroupScores> ByCaseClass,
        IReadOnlyList<GroupScores> ByValidInputStyle,
        IReadOnlyDictionary<string, int> StatusConfusionMatrix,
        IReadOnlyDictionary<string, int> ProviderFailureCounts,
        LatencyScores Latency,
        UsageTotals Usage,
        IReadOnlyList<CaseOutcome> Cases)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public V2ReportMetadata? V2 { get; init; }
    }

    internal sealed record ProviderUsageAccounting(
        UsageTotals Totals,
        long? CachedPromptTokens,
        int CachedPromptTokenResponses,
        long? CacheMissPromptTokens,
        int CacheMissPromptTokenResponses,
        long? ReasoningTokens,
        int ReasoningTokenResponses,
        bool BaseUsageComplete,
        string CostStatus,
        string CostBasis,
        decimal? EstimatedCostUsd);

    internal sealed record ProviderCostEstimate(
        decimal? EstimatedCostUsd,
        string Status,
        string Basis);

    internal sealed record ProviderRunConfiguration(
        string Provider,
        string Model,
        string? ApiVersion,
        string ReasoningSetting,
        double? Temperature);

    internal sealed record ProviderRunSummary(
        string Provider,
        string Model,
        string? ApiVersion,
        string PromptVersion,
        string PromptFingerprint,
        int SchemaVersion,
        double? Temperature,
        string ReasoningSetting,
        int TimeoutSeconds,
        int MaximumOutputTokens,
        int MaximumResponseBytes,
        int MaximumRequests,
        int ActualAttempts,
        int NotExecutedCases,
        int HttpSuccessResponses,
        int ResponsesWithText,
        int ProviderFailures,
        string? TerminalFailureCode,
        bool EvaluationComplete,
        bool UsageComplete,
        long? CachedPromptTokens,
        int CachedPromptTokenResponses,
        long? CacheMissPromptTokens,
        int CacheMissPromptTokenResponses,
        long? ReasoningTokens,
        int ReasoningTokenResponses,
        string CostStatus,
        string CostBasis,
        LatencyScores ProviderLatency,
        IReadOnlyList<ProviderCallSummary> Calls);

    internal sealed record ProviderCallSummary(
        int Attempt,
        bool HttpSucceeded,
        bool HasText,
        double LatencyMilliseconds,
        string? ReportedModelVersion,
        string? SystemFingerprint,
        string? SafeErrorCode,
        NaturalLanguageAnalyticsProviderUsage? Usage);

    internal sealed record CaseScoreBreakdown(
        int Cases,
        MetricScore StatusAccuracy,
        MetricScore CompletePlanAccuracyOnExpectedValid,
        IReadOnlyDictionary<string, MetricScore> ValidFieldAccuracy,
        MetricScore ExactClarificationFieldsOnExpectedClarification,
        MetricScore UnsupportedRejectionOnExpectedUnsupported,
        MetricScore AdversarialRejection,
        int NonValidExecutableOutputCount);

    internal sealed record MetricBreakdown(string Metric, CaseScoreBreakdown Scores);

    internal sealed record V2ReportMetadata(
        string DatasetVersion,
        string DatasetStatus,
        bool HumanReviewCompleted,
        bool HeldoutEvaluationAuthorized,
        bool EvaluationComplete,
        int ProposedDevelopmentCases,
        int ProposedHeldoutCases,
        int ProposedDevelopmentFamilies,
        int ProposedHeldoutFamilies,
        int PlannedCases,
        int EvaluatedCases,
        int NotExecutedCases,
        IReadOnlyDictionary<string, CaseScoreBreakdown> ByLanguage,
        IReadOnlyDictionary<string, CaseScoreBreakdown> ByInputStyle,
        IReadOnlyDictionary<string, CaseScoreBreakdown> ByExpectedStatus,
        IReadOnlyList<MetricBreakdown> ByExpectedMetric,
        MetricScore AdversarialRejection,
        ProviderRunSummary? ProviderRun);

    private sealed class FixedOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        public TOptions CurrentValue => value;

        public TOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }

    internal sealed class EvaluationSetupException(string code) : Exception
    {
        public string Code { get; } = code;
    }
}
