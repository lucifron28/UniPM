using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniPM.Api.Features.Reports;

namespace UniPM.PmAnalytics.V3Evaluation;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] is not ("validate" or "diagnose" or "score"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- <validate|diagnose|score>");
            return 2;
        }

        string root;
        try
        {
            root = V3EvaluationHarness.FindRepositoryRoot();
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        object result;
        try
        {
            result = args[0] switch
            {
                "validate" => V3EvaluationHarness.Validate(root),
                "diagnose" => V3EvaluationHarness.Diagnose(root),
                "score" => V3EvaluationHarness.Score(root),
                _ => throw new InvalidOperationException()
            };
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        Console.WriteLine(JsonSerializer.Serialize(result, V3EvaluationHarness.OutputJsonOptions));
        return result switch
        {
            V3CorpusValidation validation => validation.ExitCode,
            V3Diagnosis diagnosis => diagnosis.ExitCode,
            V3ScoreGate gate => gate.ExitCode,
            _ => 2
        };
    }
}

internal sealed record V3CorpusValidation(
    bool IsValid,
    int CaseCount,
    int PendingReviewCount,
    IReadOnlyList<string> Errors,
    string Message)
{
    public int ExitCode => IsValid ? 0 : 1;
}

internal sealed record V3CaseDiagnostic(
    string CaseId,
    string CaseClass,
    string IntentFamily,
    string ExpectedStatus,
    string ActualStatus,
    IReadOnlyList<string> ExpectedClarificationFields,
    IReadOnlyList<string> ActualClarificationFields,
    string ExpectedPlanDisposition,
    string ActualPlanDisposition,
    int AssertionCount,
    int MatchedAssertionCount,
    bool UnsafeAcceptance,
    double ElapsedMilliseconds,
    IReadOnlyList<string> Mismatches);

internal sealed record V3Diagnosis(
    int CaseCount,
    int CasesWithDifferences,
    string ScoringStatus,
    IReadOnlyList<V3CaseDiagnostic> Cases)
{
    public int ExitCode => 0;
}

internal sealed record V3ScoreGate(
    bool ScoreProduced,
    int PendingReviewCount,
    int UnsafeAcceptanceCount,
    int TotalCases,
    int StatusCorrect,
    decimal? StatusAccuracyPercent,
    int ClarificationSetsCorrect,
    decimal? ClarificationSetAccuracyPercent,
    int PlanDispositionsCorrect,
    decimal? PlanDispositionAccuracyPercent,
    int PlanAssertions,
    int PlanAssertionsCorrect,
    decimal? PlanAssertionAccuracyPercent,
    int CasesMatchingAllDeclaredExpectations,
    decimal? CaseExactnessPercent,
    IReadOnlyList<V3GroupedScore> ByCaseClass,
    IReadOnlyList<V3GroupedScore> ByIntentFamily,
    string Message,
    int ExitCode);

internal sealed record V3GroupedScore(
    string Group,
    int Cases,
    int StatusCorrect,
    decimal? StatusAccuracyPercent,
    int ClarificationSetsCorrect,
    decimal? ClarificationSetAccuracyPercent,
    int PlanDispositionsCorrect,
    decimal? PlanDispositionAccuracyPercent,
    int PlanAssertions,
    int PlanAssertionsCorrect,
    decimal? PlanAssertionAccuracyPercent,
    int CasesMatchingAllDeclaredExpectations,
    decimal? CaseExactnessPercent,
    double MeanInterpreterMilliseconds,
    double P95InterpreterMilliseconds);

internal static class V3EvaluationHarness
{
    private const string RelativeDirectory = "reference/evaluation/pm-analytics-v3";
    private const string CandidateFileName = "dev-candidates.jsonl";
    private const string ManifestFileName = "corpus-manifest.json";
    private const string ReviewFileName = "review-decisions.json";
    private const string FixtureFileName = "synthetic-calculation-fixtures.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static JsonSerializerOptions OutputJsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, RelativeDirectory, CandidateFileName)))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the UniPM V3 evaluation corpus from the current directory.");
    }

    internal static V3CorpusValidation Validate(string root)
    {
        var errors = new List<string>();
        var corpusDirectory = Path.Combine(root, RelativeDirectory);
        var candidatePath = Path.Combine(corpusDirectory, CandidateFileName);
        var manifestPath = Path.Combine(corpusDirectory, ManifestFileName);
        var reviewPath = Path.Combine(corpusDirectory, ReviewFileName);
        var fixturePath = Path.Combine(corpusDirectory, FixtureFileName);
        if (!File.Exists(candidatePath) || !File.Exists(manifestPath) || !File.Exists(reviewPath) || !File.Exists(fixturePath))
        {
            return new V3CorpusValidation(false, 0, 0, ["V3 candidate, manifest, review ledger, or calculation fixtures are missing."], "Corpus structure is invalid.");
        }

        var cases = ReadCases(candidatePath, errors);
        var ids = cases
            .Where(item => item.TryGetProperty("caseId", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("caseId").GetString()!)
            .ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            errors.Add("caseId values must be unique.");
        }

        ValidateManifest(manifestPath, candidatePath, cases.Count, errors);
        var pending = ValidateReviewLedger(reviewPath, ids, errors);
        ValidateFixtureSet(fixturePath, errors);
        var valid = errors.Count == 0;
        var message = valid
            ? $"V3 corpus structure is valid ({cases.Count} cases); {pending} labels remain pending review. No score was produced."
            : "Corpus structure is invalid.";
        return new V3CorpusValidation(valid, cases.Count, pending, errors, message);
    }

    internal static V3Diagnosis Diagnose(string root)
    {
        var validation = Validate(root);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(" ", validation.Errors));
        }

        var corpusDirectory = Path.Combine(root, RelativeDirectory);
        var manifest = ReadManifest(Path.Combine(corpusDirectory, ManifestFileName));
        var now = DateTimeOffset.Parse(manifest.AsOfUtc, System.Globalization.CultureInfo.InvariantCulture);
        var diagnostics = new List<V3CaseDiagnostic>();
        foreach (var item in ReadCases(Path.Combine(corpusDirectory, CandidateFileName), []))
        {
            var caseId = item.GetProperty("caseId").GetString()!;
            var expectedStatus = item.GetProperty("expectedStatus").GetString()!;
            var expectedFields = ReadStringArray(item.GetProperty("expectedClarificationFields"));
            var expectedDisposition = item.GetProperty("expectedPlanDisposition").GetString()!;
            var question = NaturalLanguageAnalyticsQuestionGuard.SanitizeForModel(item.GetProperty("question").GetString()!);
            var stopwatch = Stopwatch.StartNew();
            var actual = NaturalLanguageAnalyticsQuestionGuard.IsUnsafeModelRequest(question)
                ? new PmAnalyticsV3ModelOutput("Unsupported", null, [], "UnsafeRequestNotSupported", null)
                : PmAnalyticsV3RuleParser.Parse(question, now);
            stopwatch.Stop();
            var actualFields = actual.ClarificationFields.Order(StringComparer.Ordinal).ToArray();
            var sortedExpectedFields = expectedFields.Order(StringComparer.Ordinal).ToArray();
            var actualDisposition = GetDisposition(actual.Status, actual.Plan is not null);
            var assertion = item.GetProperty("expectedPlanAssertions");
            var mismatches = new List<string>();
            if (!string.Equals(expectedStatus, actual.Status, StringComparison.Ordinal))
            {
                mismatches.Add("status");
            }

            if (!sortedExpectedFields.SequenceEqual(actualFields, StringComparer.Ordinal))
            {
                mismatches.Add("clarificationFields");
            }

            if (!string.Equals(expectedDisposition, actualDisposition, StringComparison.Ordinal))
            {
                mismatches.Add("planDisposition");
            }

            var unsafeAcceptance = expectedStatus == "Unsupported"
                && (actual.Status != "Unsupported" || actual.Plan is not null);
            if (unsafeAcceptance)
            {
                mismatches.Add("unsupportedSafety");
            }

            var assertionCount = 0;
            var matchedAssertionCount = 0;
            if (assertion.ValueKind == JsonValueKind.Object)
            {
                using var actualPlan = JsonDocument.Parse(JsonSerializer.Serialize(actual.Plan, JsonOptions));
                ComparePartial(assertion, actualPlan.RootElement, "plan", mismatches, ref assertionCount, ref matchedAssertionCount);
            }

            diagnostics.Add(new V3CaseDiagnostic(
                caseId,
                item.GetProperty("caseClass").GetString()!,
                item.GetProperty("intentFamily").GetString()!,
                expectedStatus,
                actual.Status,
                sortedExpectedFields,
                actualFields,
                expectedDisposition,
                actualDisposition,
                assertionCount,
                matchedAssertionCount,
                unsafeAcceptance,
                stopwatch.Elapsed.TotalMilliseconds,
                mismatches));
        }

        var differences = diagnostics.Count(item => item.Mismatches.Count > 0);
        return new V3Diagnosis(
            diagnostics.Count,
            differences,
            validation.PendingReviewCount == 0 ? "Eligible only if manifest is frozen and reviews are complete." : "Blocked: provisional labels are pending human review; diagnostic differences are not accuracy scores.",
            diagnostics);
    }

    internal static V3ScoreGate Score(string root)
    {
        var validation = Validate(root);
        if (!validation.IsValid)
        {
            return Blocked(validation.PendingReviewCount, 0, string.Join(" ", validation.Errors), 1);
        }

        var corpusDirectory = Path.Combine(root, RelativeDirectory);
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(corpusDirectory, ManifestFileName)));
        var manifestRoot = manifestDocument.RootElement;
        var isFrozen = string.Equals(manifestRoot.GetProperty("status").GetString(), "frozen", StringComparison.Ordinal);
        var freezeHash = manifestRoot.GetProperty("freezeSha256").GetString();
        var candidateHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(corpusDirectory, CandidateFileName)))).ToLowerInvariant();
        var hashMatches = isFrozen && string.Equals(freezeHash, candidateHash, StringComparison.Ordinal);
        if (validation.PendingReviewCount != 0 || !hashMatches)
        {
            var reason = validation.PendingReviewCount != 0
                ? $"Scoring is blocked: {validation.PendingReviewCount} of {validation.CaseCount} labels still require human review."
                : "Scoring is blocked: the reviewed corpus is not frozen to its current SHA-256.";
            return Blocked(validation.PendingReviewCount, 0, reason, 3);
        }

        var diagnosis = Diagnose(root);
        var unsafeAcceptanceCount = diagnosis.Cases.Count(item => item.UnsafeAcceptance);
        if (unsafeAcceptanceCount > 0)
        {
            return Blocked(0, unsafeAcceptanceCount, "Scoring is blocked by accepted adversarial requests. Resolve every unsupported-safety failure first.", 4);
        }

        var cases = diagnosis.Cases;
        var statusCorrect = cases.Count(item => item.ExpectedStatus == item.ActualStatus);
        var clarificationCorrect = cases.Count(item => item.ExpectedClarificationFields.SequenceEqual(item.ActualClarificationFields, StringComparer.Ordinal));
        var dispositionCorrect = cases.Count(item => item.ExpectedPlanDisposition == item.ActualPlanDisposition);
        var assertionCount = cases.Sum(item => item.AssertionCount);
        var assertionMatches = cases.Sum(item => item.MatchedAssertionCount);
        var exactCases = cases.Count(item => item.Mismatches.Count == 0);
        return new V3ScoreGate(
            true,
            0,
            0,
            cases.Count,
            statusCorrect,
            AccuracyPercent(statusCorrect, cases.Count),
            clarificationCorrect,
            AccuracyPercent(clarificationCorrect, cases.Count),
            dispositionCorrect,
            AccuracyPercent(dispositionCorrect, cases.Count),
            assertionCount,
            assertionMatches,
            AccuracyPercent(assertionMatches, assertionCount),
            exactCases,
            AccuracyPercent(exactCases, cases.Count),
            GroupScores(cases, item => item.CaseClass),
            GroupScores(cases, item => item.IntentFamily),
            "Scored reviewed, frozen deterministic V3 labels. No provider was called; this is not held-out or GSD acceptance evidence.",
            0);
    }

    private static V3ScoreGate Blocked(int pendingReviews, int unsafeAcceptances, string message, int exitCode) => new(
        false,
        pendingReviews,
        unsafeAcceptances,
        0,
        0,
        null,
        0,
        null,
        0,
        null,
        0,
        0,
        null,
        0,
        null,
        [],
        [],
        message,
        exitCode);

    private static IReadOnlyList<V3GroupedScore> GroupScores(
        IReadOnlyList<V3CaseDiagnostic> cases,
        Func<V3CaseDiagnostic, string> groupSelector) => cases
        .GroupBy(groupSelector, StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .Select(group => new V3GroupedScore(
            group.Key,
            group.Count(),
            group.Count(item => item.ExpectedStatus == item.ActualStatus),
            AccuracyPercent(group.Count(item => item.ExpectedStatus == item.ActualStatus), group.Count()),
            group.Count(item => item.ExpectedClarificationFields.SequenceEqual(item.ActualClarificationFields, StringComparer.Ordinal)),
            AccuracyPercent(group.Count(item => item.ExpectedClarificationFields.SequenceEqual(item.ActualClarificationFields, StringComparer.Ordinal)), group.Count()),
            group.Count(item => item.ExpectedPlanDisposition == item.ActualPlanDisposition),
            AccuracyPercent(group.Count(item => item.ExpectedPlanDisposition == item.ActualPlanDisposition), group.Count()),
            group.Sum(item => item.AssertionCount),
            group.Sum(item => item.MatchedAssertionCount),
            AccuracyPercent(group.Sum(item => item.MatchedAssertionCount), group.Sum(item => item.AssertionCount)),
            group.Count(item => item.Mismatches.Count == 0),
            AccuracyPercent(group.Count(item => item.Mismatches.Count == 0), group.Count()),
            group.Average(item => item.ElapsedMilliseconds),
            Percentile95(group.Select(item => item.ElapsedMilliseconds))))
        .ToArray();

    private static decimal? AccuracyPercent(int correct, int total) => total == 0
        ? null
        : correct * 100m / total;

    private static double Percentile95(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return 0;
        var index = Math.Max(0, (int)Math.Ceiling(ordered.Length * 0.95) - 1);
        return ordered[index];
    }

    private static List<JsonElement> ReadCases(string path, ICollection<string> errors)
    {
        var cases = new List<JsonElement>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                errors.Add($"Line {lineNumber} is blank.");
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var item = document.RootElement.Clone();
                ValidateCase(item, lineNumber, errors);
                cases.Add(item);
            }
            catch (JsonException)
            {
                errors.Add($"Line {lineNumber} is not valid JSON.");
            }
        }

        return cases;
    }

    private static void ValidateCase(JsonElement item, int lineNumber, ICollection<string> errors)
    {
        var prefix = $"Line {lineNumber}";
        if (item.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{prefix} must be a JSON object.");
            return;
        }

        var requiredProperties = new[] { "caseId", "intentFamily", "caseClass", "question", "expectedStatus", "expectedClarificationFields", "expectedPlanDisposition", "executablePlanAllowed", "expectedPlanAssertions" };
        foreach (var property in requiredProperties)
        {
            if (!item.TryGetProperty(property, out _)) errors.Add($"{prefix} is missing '{property}'.");
        }

        if (requiredProperties.Any(property => !item.TryGetProperty(property, out _))) return;

        if (!item.TryGetProperty("caseId", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
            errors.Add($"{prefix} has an invalid caseId.");
        if (!item.TryGetProperty("question", out var question) || question.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(question.GetString()))
            errors.Add($"{prefix} has an invalid question.");
        if (!item.TryGetProperty("expectedStatus", out var status) || status.ValueKind != JsonValueKind.String || status.GetString() is not ("Valid" or "NeedsClarification" or "Unsupported"))
            errors.Add($"{prefix} has an unsupported expectedStatus.");
        if (!item.TryGetProperty("expectedClarificationFields", out var fields) || fields.ValueKind != JsonValueKind.Array || fields.EnumerateArray().Any(field => field.ValueKind != JsonValueKind.String))
            errors.Add($"{prefix} expectedClarificationFields must be a string array.");
        if (!item.TryGetProperty("expectedPlanDisposition", out var disposition) || disposition.ValueKind != JsonValueKind.String || disposition.GetString() is not ("Executable" or "ClarificationDraft" or "None"))
            errors.Add($"{prefix} has an unsupported expectedPlanDisposition.");
        if (!item.TryGetProperty("executablePlanAllowed", out var allowed) || allowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            errors.Add($"{prefix} executablePlanAllowed must be Boolean.");

        if (item.TryGetProperty("expectedPlanAssertions", out var assertions)
            && assertions.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
            errors.Add($"{prefix} expectedPlanAssertions must be an object or null.");

        if (fields.ValueKind == JsonValueKind.Array)
        {
            var clarificationFields = ReadStringArray(fields);
            if (clarificationFields.Distinct(StringComparer.Ordinal).Count() != clarificationFields.Length)
                errors.Add($"{prefix} clarification fields must be unique.");
            if (clarificationFields.Any(field => !PmAnalyticsV3Catalog.ClarificationFields.Contains(field, StringComparer.Ordinal)))
                errors.Add($"{prefix} includes a clarification field outside the V3 catalog.");
            if (status.ValueKind == JsonValueKind.String
                && (status.GetString() == "NeedsClarification") != (clarificationFields.Length > 0))
                errors.Add($"{prefix} clarification fields conflict with expectedStatus.");
        }

        if (status.ValueKind == JsonValueKind.String && disposition.ValueKind == JsonValueKind.String)
        {
            var expectedStatus = status.GetString();
            var expectedDisposition = disposition.GetString();
            var shouldAllowExecution = expectedStatus == "Valid" && expectedDisposition == "Executable";
            if (allowed.ValueKind is JsonValueKind.True or JsonValueKind.False && allowed.GetBoolean() != shouldAllowExecution)
                errors.Add($"{prefix} executablePlanAllowed conflicts with the expected status/disposition.");
            if ((expectedStatus == "Valid") != (expectedDisposition == "Executable"))
                errors.Add($"{prefix} expected status and plan disposition conflict.");
            if (expectedStatus == "Unsupported" && expectedDisposition != "None")
                errors.Add($"{prefix} unsupported cases must expect no plan.");
            if (expectedDisposition == "ClarificationDraft"
                && (expectedStatus != "NeedsClarification" || fields.GetArrayLength() == 0 || assertions.ValueKind != JsonValueKind.Object))
                errors.Add($"{prefix} clarification drafts require clarification fields and plan assertions.");
            if (expectedDisposition == "None" && assertions.ValueKind != JsonValueKind.Null)
                errors.Add($"{prefix} no-plan dispositions must have null plan assertions.");
            if (expectedDisposition == "Executable" && assertions.ValueKind != JsonValueKind.Object)
                errors.Add($"{prefix} executable dispositions require plan assertions.");
        }
    }

    private static void ValidateFixtureSet(string fixturePath, ICollection<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
            var root = document.RootElement;
            if (root.GetProperty("fixtureSetId").GetString() != "NLA-V3-SYNTHETIC-CALCULATIONS-001")
            {
                errors.Add("Calculation fixture set ID is not the registered V3 synthetic set.");
            }

            var fixtures = root.GetProperty("fixtures").EnumerateArray().ToArray();
            var ids = new List<string>();
            foreach (var fixture in fixtures)
            {
                var id = fixture.GetProperty("fixtureId").GetString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add("Calculation fixtures require a non-empty fixtureId.");
                }
                else
                {
                    ids.Add(id);
                }

                _ = DateTimeOffset.Parse(fixture.GetProperty("asOfUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                if (fixture.GetProperty("assets").ValueKind != JsonValueKind.Array || fixture.GetProperty("expected").ValueKind != JsonValueKind.Object)
                {
                    errors.Add($"Calculation fixture '{id}' must contain an assets array and expected object.");
                    continue;
                }

                foreach (var asset in fixture.GetProperty("assets").EnumerateArray())
                {
                    foreach (var property in new[] { "assetCode", "category", "department", "building", "cycle", "completedAt", "isOperational" })
                    {
                        if (!asset.TryGetProperty(property, out _)) errors.Add($"Calculation fixture '{id}' asset is missing '{property}'.");
                    }
                }
            }

            var requiredIds = new[]
            {
                "denominator-matching", "grouped-threshold", "category-cycles",
                "quarter-source-records", "records-pagination", "missing-category-coverage", "zero-eligible"
            };
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count
                || !requiredIds.Order(StringComparer.Ordinal).SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                errors.Add("Calculation fixture IDs must be unique and cover all seven required scenarios.");
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            errors.Add("Calculation fixture set is missing required fields or contains invalid values.");
        }
    }

    private static void ValidateManifest(string manifestPath, string candidatePath, int caseCount, ICollection<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var manifest = document.RootElement;
            if (manifest.GetProperty("protocolId").GetString() != "NLA-V3-EVAL-PROTOCOL-001") errors.Add("Manifest protocolId does not match the V3 protocol.");
            if (manifest.GetProperty("corpusId").GetString() != "NLA-V3-DEV-CANDIDATES-001") errors.Add("Manifest corpusId is not the registered V3 development corpus.");
            if (manifest.GetProperty("candidateFile").GetString() != CandidateFileName) errors.Add("Manifest candidateFile must point only at the V3 development candidate file.");
            if (manifest.GetProperty("status").GetString() is not ("draft" or "frozen")) errors.Add("Manifest status must be draft or frozen.");
            if (manifest.GetProperty("heldoutIncluded").GetBoolean()) errors.Add("The V3 development evaluation cannot include held-out data.");
            if (manifest.GetProperty("expectedCaseCount").GetInt32() != caseCount) errors.Add("Manifest expectedCaseCount does not match the candidate file.");
            var fileHash = manifest.GetProperty("fileSha256").GetString();
            var currentHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidatePath))).ToLowerInvariant();
            if (fileHash != currentHash) errors.Add("Manifest fileSha256 does not match the current candidate file.");
            if (manifest.GetProperty("status").GetString() == "frozen")
            {
                var freezeHash = manifest.GetProperty("freezeSha256").GetString();
                if (!string.Equals(freezeHash, currentHash, StringComparison.Ordinal)) errors.Add("Frozen manifest freezeSha256 does not match the candidate file.");
                if (manifest.GetProperty("schemaVersion").GetInt32() != 3
                    || manifest.GetProperty("catalogVersion").GetInt32() != PmAnalyticsV3Catalog.Version
                    || manifest.GetProperty("interpreterId").GetString() != "PmAnalyticsV3QuestionGuard+PmAnalyticsV3RuleParser")
                    errors.Add("A frozen V3 manifest must pin its schema, catalog, and deterministic interpreter identity.");
                var sourceCommit = manifest.GetProperty("frozenSourceCommit").GetString();
                if (sourceCommit is null || sourceCommit.Length != 40 || !sourceCommit.All(Uri.IsHexDigit))
                    errors.Add("A frozen V3 manifest must pin the full source commit SHA.");
                if (!manifest.GetProperty("reviewed").GetBoolean())
                    errors.Add("A frozen V3 manifest must be explicitly marked reviewed.");
            }

            _ = DateTimeOffset.Parse(manifest.GetProperty("asOfUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            errors.Add("Manifest is missing required fields or contains invalid values.");
        }
    }

    private static int ValidateReviewLedger(string reviewPath, IReadOnlyCollection<string> ids, ICollection<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(reviewPath));
            var root = document.RootElement;
            if (root.GetProperty("corpusId").GetString() != "NLA-V3-DEV-CANDIDATES-001") errors.Add("Review ledger corpusId does not match the V3 development corpus.");
            var decisions = root.GetProperty("decisions").EnumerateArray().ToArray();
            var decisionIds = decisions.Select(item => item.GetProperty("caseId").GetString() ?? string.Empty).ToArray();
            if (decisionIds.Distinct(StringComparer.Ordinal).Count() != decisionIds.Length || !ids.Order(StringComparer.Ordinal).SequenceEqual(decisionIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                errors.Add("Review ledger must contain exactly one decision row for each candidate case.");

            foreach (var decision in decisions)
            {
                var status = decision.GetProperty("status").GetString();
                if (status is not ("pending" or "approved" or "rejected")) errors.Add("Review ledger decision status is invalid.");
                if (status == "approved")
                {
                    var reviewers = decision.GetProperty("reviewers").EnumerateArray().ToArray();
                    var reviewDate = decision.GetProperty("reviewDate").GetString();
                    if (reviewers.Length == 0
                        || reviewers.Any(reviewer => reviewer.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reviewer.GetString()))
                        || !DateOnly.TryParseExact(reviewDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                        errors.Add("Approved case decisions require reviewers and a review date.");
                }
            }

            return decisions.Count(item => item.GetProperty("status").GetString() != "approved");
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            errors.Add("Review ledger is missing required fields or contains invalid values.");
            return ids.Count;
        }
    }

    private static CorpusManifest ReadManifest(string path) => JsonSerializer.Deserialize<CorpusManifest>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException("V3 manifest is empty.");

    private static string GetDisposition(string status, bool hasPlan) =>
        status == "Valid" && hasPlan ? "Executable"
            : status == "NeedsClarification" && hasPlan ? "ClarificationDraft"
            : "None";

    private static string[] ReadStringArray(JsonElement value) => value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

    private static void ComparePartial(JsonElement expected, JsonElement actual, string path, ICollection<string> mismatches, ref int assertionCount, ref int matchedAssertionCount)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in expected.EnumerateObject())
            {
                if (actual.ValueKind == JsonValueKind.Object && actual.TryGetProperty(property.Name, out var actualValue))
                {
                    ComparePartial(property.Value, actualValue, $"{path}.{property.Name}", mismatches, ref assertionCount, ref matchedAssertionCount);
                }
                else
                {
                    assertionCount += CountAssertions(property.Value);
                    mismatches.Add($"{path}.{property.Name}");
                }
            }
            return;
        }

        if (expected.ValueKind == JsonValueKind.Array)
        {
            var expectedItems = expected.EnumerateArray().ToArray();
            var actualItems = actual.ValueKind == JsonValueKind.Array ? actual.EnumerateArray().ToArray() : [];
            assertionCount++;
            if (!expectedItems.Select(CanonicalJson).SequenceEqual(actualItems.Select(CanonicalJson), StringComparer.Ordinal))
                mismatches.Add(path);
            else
                matchedAssertionCount++;
            return;
        }

        assertionCount++;
        if (JsonElement.DeepEquals(expected, actual)) matchedAssertionCount++;
        else mismatches.Add(path);
    }

    private static int CountAssertions(JsonElement element) => element.ValueKind == JsonValueKind.Object
        ? element.EnumerateObject().Sum(property => CountAssertions(property.Value))
        : 1;

    private static string CanonicalJson(JsonElement value) => JsonSerializer.Serialize(value);

    private sealed record CorpusManifest(string ProtocolId, string CorpusId, string Status, string AsOfUtc);
}
