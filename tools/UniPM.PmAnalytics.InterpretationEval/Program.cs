using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UniPM.Api.Features.Reports;

namespace UniPM.PmAnalytics.InterpretationEval;

internal static class Program
{
    private const string DatasetRelativePath = "reference/evaluation/pm-analytics-interpretation/v1/cases.jsonl";
    private const string ManifestFileName = "split-manifest.md";
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
            var root = FindRepositoryRoot();
            arguments.SourceSha = await GitSourceShaVerifier.VerifyAsync(
                root,
                arguments.SourceSha);
            var datasetPath = arguments.DatasetPath is null
                ? Path.Combine(root, DatasetRelativePath)
                : Path.GetFullPath(arguments.DatasetPath);
            var selection = await ReadCasesAsync(datasetPath, arguments.Split);
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
            try
            {
                INaturalLanguageAnalyticsInterpreter interpreter;
                ModelMetadata? model = null;
                if (arguments.Mode == "rule-based")
                {
                    interpreter = new RuleBasedNaturalLanguageAnalyticsInterpreter();
                }
                else
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

                var startedAt = DateTimeOffset.UtcNow;
                var outcomes = await EvaluateAsync(interpreter, selection.Cases);
                var completedAt = DateTimeOffset.UtcNow;
                var report = BuildReport(
                    arguments,
                    selection,
                    outcomes,
                    model,
                    naturalLanguageOptions,
                    startedAt,
                    completedAt);

                var outputPath = Path.GetFullPath(arguments.OutputPath ??
                    Path.Combine(
                        root,
                        "artifacts",
                        "evaluation",
                        "pm-analytics-interpretation",
                        $"{arguments.Split}-{startedAt:yyyyMMdd'T'HHmmss'Z'}.json"));
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
        IReadOnlyList<EvaluationCase> cases)
    {
        var outcomes = new List<CaseOutcome>(cases.Count);
        foreach (var item in cases)
        {
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

    private static CaseOutcome ScoreCase(
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

    private static EvaluationReport BuildReport(
        EvaluationArguments arguments,
        DatasetSelection selection,
        IReadOnlyList<CaseOutcome> outcomes,
        ModelMetadata? model,
        NaturalLanguageAnalyticsOptions configuration,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        var cases = selection.Cases;
        var valid = cases.Where(item => item.Expected.Status == "Valid").ToArray();
        var clarifications = cases.Where(item => item.Expected.Status == "NeedsClarification").ToArray();
        var unsupported = cases.Where(item => item.Expected.Status == "Unsupported").ToArray();
        var strictControls = outcomes.Where(item => item.CaseTag == "strict-template-control").ToArray();
        var freePhrasing = outcomes.Where(item =>
            item.ExpectedStatus == "Valid" && item.CaseTag != "strict-template-control").ToArray();

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
        var promptTokens = modelResponses.Where(item => item.ProviderUsage!.PromptTokens.HasValue)
            .Select(item => item.ProviderUsage!.PromptTokens!.Value).ToArray();
        var completionTokens = modelResponses.Where(item => item.ProviderUsage!.CompletionTokens.HasValue)
            .Select(item => item.ProviderUsage!.CompletionTokens!.Value).ToArray();
        var providerDurations = modelResponses.Where(item => item.ProviderUsage!.DurationNanoseconds.HasValue)
            .Select(item => item.ProviderUsage!.DurationNanoseconds!.Value).ToArray();
        var latency = outcomes.Select(item => item.LatencyMilliseconds).Order().ToArray();
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
            : null;

        return new EvaluationReport(
            1,
            arguments.Split,
            arguments.Mode,
            arguments.SourceSha,
            selection.Sha256,
            startedAt,
            completedAt,
            modelConfiguration,
            new ExecutionCounts(
                cases.Count,
                outcomes.Count(item => item.EvaluatorError is null),
                modelResponses.Length + providerErrors,
                modelResponses.Length,
                providerErrors,
                outcomes.Count(item => item.EvaluatorError is not null),
                arguments.Mode == "ollama" ? guardedCases : null,
                arguments.Mode == "rule-based" ? guardedCases : null),
            new OverallScores(
                Score(outcomes.Count(item => item.StatusCorrect), cases.Count),
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
            BuildGroups(outcomes, item => item.Language),
            BuildGroups(outcomes, item => item.CaseClass),
            BuildGroups(outcomes.Where(item => item.ExpectedStatus == "Valid"), item =>
                item.CaseTag == "strict-template-control" ? "strict-template-control" : "free-phrasing"),
            BuildConfusion(outcomes),
            BuildProviderErrors(outcomes),
            new LatencyScores(
                latency.Length,
                Percentile(latency, 0.50),
                Percentile(latency, 0.95)),
            new UsageTotals(
                SumIfComplete(promptTokens, modelResponses.Length),
                promptTokens.Length,
                SumIfComplete(completionTokens, modelResponses.Length),
                completionTokens.Length,
                SumIfComplete(providerDurations, modelResponses.Length),
                providerDurations.Length,
                null),
            outcomes);
    }

    private static IReadOnlyList<GroupScores> BuildGroups(
        IEnumerable<CaseOutcome> outcomes,
        Func<CaseOutcome, string> key)
        => outcomes.GroupBy(key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new GroupScores(
                group.Key,
                group.Count(),
                group.Count(item => item.ProviderError is null && item.EvaluatorError is null),
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
                or "QuestionNotSupported"
                or "UnsupportedPmCycle"
                or "UnsafeDepartment"
                or "YearRequired"
                or "MonthRequired"
                ? code
                : "Other";

    private static async Task<DatasetSelection> ReadCasesAsync(string datasetPath, string split)
    {
        if (!File.Exists(datasetPath))
        {
            throw new EvaluationSetupException("DatasetNotFound");
        }

        var bytes = await File.ReadAllBytesAsync(datasetPath);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifestPath = Path.Combine(Path.GetDirectoryName(datasetPath)!, ManifestFileName);
        EnsureManifestHash(manifestPath, sha256);

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

            if (familySplits.TryGetValue(item.FamilyId, out var familySplit)
                && familySplit != item.Split)
            {
                throw new EvaluationSetupException("FamilyCrossesSplits");
            }

            familySplits[item.FamilyId] = item.Split;
            ValidateExpected(item, lineNumber);
            allCases.Add(item);
        }

        if (allCases.Count != 100)
        {
            throw new EvaluationSetupException("UnexpectedDatasetCount");
        }

        var selected = allCases.Where(item => item.Split == split).ToArray();
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

        return new DatasetSelection(selected, sha256);
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

    private static bool TryParseArguments(
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
            "--split", "--mode", "--source-sha", "--model", "--base-address", "--dataset", "--output"
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

        if (!values.TryGetValue("--mode", out var mode) || mode is not ("rule-based" or "ollama"))
        {
            error = "Specify --mode rule-based or --mode ollama.";
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
        Required: --split dev|heldout --mode rule-based|ollama --source-sha <full-commit-sha>
        Optional: --model <local-model-tag> --base-address <loopback-ollama-url>
                  --dataset <cases.jsonl> --output <report.json>
        """;

    private sealed class EvaluationArguments
    {
        public bool ShowHelp { get; set; }
        public string Split { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string SourceSha { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string BaseAddress { get; set; } = string.Empty;
        public string? DatasetPath { get; set; }
        public string? OutputPath { get; set; }
    }

    private sealed record EvaluationCase(
        string CaseId,
        string FamilyId,
        string Split,
        string Language,
        string CaseClass,
        string Question,
        ExpectedLabel Expected,
        string? CaseTag);

    private sealed record ExpectedLabel(
        string Status,
        ExpectedPlan? Plan,
        string[] ClarificationFields,
        string? Presentation);

    private sealed record ExpectedPlan(
        string Metric,
        string AssetCategory,
        string PmCycle,
        string? Department,
        string GroupBy);

    private sealed record SafePlan(
        string Metric,
        string AssetCategory,
        string PmCycle,
        string? Department,
        string GroupBy);

    private sealed record FieldScores(
        bool Metric,
        bool AssetCategory,
        bool PmCycle,
        bool Department,
        bool GroupBy,
        bool Presentation);

    private sealed record CaseOutcome(
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
        bool NonValidExecutableOutput)
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
    }

    private sealed record DatasetSelection(IReadOnlyList<EvaluationCase> Cases, string Sha256);

    private sealed record MetricScore(int Correct, int Denominator, decimal? Rate);

    private sealed record GroupScores(
        string Group,
        int Cases,
        int Interpreted,
        int ProviderErrors,
        int EvaluatorErrors,
        MetricScore StatusAccuracy);

    private sealed record OverallScores(
        MetricScore StatusAccuracy,
        MetricScore CompletePlanAccuracyOnExpectedValid,
        IReadOnlyDictionary<string, MetricScore> ValidFieldAccuracy,
        MetricScore ExactClarificationFieldsOnExpectedClarification,
        MetricScore UnsupportedRejectionOnExpectedUnsupported,
        int NonValidExecutableOutputCount);

    private sealed record InputStyleScores(
        MetricScore StrictTemplateControlStatusAccuracy,
        MetricScore StrictTemplateControlPlanAccuracy,
        MetricScore FreePhrasingValidStatusAccuracy,
        MetricScore FreePhrasingValidPlanAccuracy);

    private sealed record LatencyScores(
        int SampleCount,
        double? P50Milliseconds,
        double? P95Milliseconds);

    private sealed record UsageTotals(
        long? PromptTokens,
        int PromptTokenResponses,
        long? CompletionTokens,
        int CompletionTokenResponses,
        long? ProviderDurationNanoseconds,
        int ProviderDurationResponses,
        decimal? Cost);

    private sealed record ModelMetadata(string Model, string Digest, string RuntimeVersion);

    private sealed record ModelConfiguration(
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

    private sealed record ExecutionCounts(
        int Cases,
        int EvaluatedCases,
        int ProviderAttempts,
        int SuccessfulModelResponses,
        int ProviderErrors,
        int EvaluatorErrors,
        int? GuardedCases,
        int? RuleBasedCases);

    private sealed record EvaluationReport(
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
        IReadOnlyList<CaseOutcome> Cases);

    private sealed class FixedOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
        where TOptions : class
    {
        public TOptions CurrentValue => value;

        public TOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }

    private sealed class EvaluationSetupException(string code) : Exception
    {
        public string Code { get; } = code;
    }
}
