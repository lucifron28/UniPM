using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.Reports;
using UniPM.Api.Models;

namespace UniPM.PmAnalytics.V3Evaluation;

// A development-only experiment. No reports, live database reads, or raw payload logging.
internal static class DeepSeekDevelopmentEvaluation
{
    private const string CorpusDirectory = "reference/evaluation/pm-analytics-v3";
    private const string FrozenCorpusHash = "dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25";
    private const int MaximumRequests = 18;
    private const decimal BudgetUsd = 1m;
    // Conservative peak, uncached rates checked on 2026-10-23. Not an invoice.
    private const decimal InputRate = 0.30m;
    private const decimal OutputRate = 1.20m;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static async Task<int> RunAsync(string outputDirectory)
    {
        var root = V3EvaluationHarness.FindRepositoryRoot();
        var validation = V3EvaluationHarness.Validate(root);
        if (!validation.IsValid || validation.PendingReviewCount != 0 || validation.CaseCount != 18)
            throw new InvalidDataException("The reviewed 18-case corpus is required.");
        var corpusPath = Path.Combine(root, CorpusDirectory, "dev-candidates.jsonl");
        if (HashFile(corpusPath) != FrozenCorpusHash)
            throw new InvalidDataException("The approved corpus has changed. No provider calls are allowed.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, CorpusDirectory, "corpus-manifest.json")));
        if (manifest.RootElement.GetProperty("status").GetString() != "frozen")
            throw new InvalidDataException("The corpus must remain frozen.");
        var now = DateTimeOffset.Parse(manifest.RootElement.GetProperty("asOfUtc").GetString()!, CultureInfo.InvariantCulture);
        var key = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Backend API key unavailable.");
        var sourceSha = Git(root, "rev-parse", "HEAD");
        if (Git(root, "status", "--porcelain").Length != 0)
            throw new InvalidOperationException("Commit the evaluator and protocol before execution.");
        outputDirectory = Path.GetFullPath(outputDirectory);
        var artifactRoot = Path.GetFullPath(Path.Combine(root, "artifacts")) + Path.DirectorySeparatorChar;
        if (!outputDirectory.StartsWith(artifactRoot, StringComparison.OrdinalIgnoreCase) || Directory.Exists(outputDirectory))
            throw new InvalidOperationException("Use a new directory under this checkout's ignored artifacts directory.");
        Directory.CreateDirectory(outputDirectory);
        var startedAt = DateTimeOffset.UtcNow;
        var clock = new FrozenTimeProvider(now);
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("DeepSeekV3Evaluation-" + Guid.NewGuid()).Options;
        var factory = new MemoryFactory(dbOptions);
        await using (var context = factory.CreateDbContext())
        {
            // Fictional lookup only. Research is deliberately absent for V3D-015.
            context.Assets.Add(new Asset
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                AssetCode = "SYNTHETIC-EVAL-001", AssetCategory = "fire-extinguisher",
                Building = "North Block", Department = "Synthetic Department", Status = "Active"
            });
            await context.SaveChangesAsync();
        }
        var query = new PmAnalyticsV3QueryService(factory, clock);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DEEPSEEK_API_KEY"] = key }).Build();
        using var interpreter = new PmAnalyticsV3QuestionInterpreter(
            new EvaluationOptions(), new DevelopmentEnvironment(), clock, query, configuration);
        var ledger = new NaturalLanguageAnalyticsModelClientRunLedger();
        using var handler = new SingleAttemptHandler(outputDirectory);
        var client = new DeepSeekNaturalLanguageAnalyticsModelClient(key, ledger, handler: handler,
            promptProfile: PmAnalyticsV3ModelPrompt.Profile);
        // The same technique as the retained live smoke runner. Production code stays unchanged.
        var clientField = typeof(PmAnalyticsV3QuestionInterpreter).GetField("_modelClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((IDisposable)clientField.GetValue(interpreter)!).Dispose();
        clientField.SetValue(interpreter, client);
        var identity = new
        {
            sourceSha, sourceBranch = Git(root, "branch", "--show-current"), startedAtUtc = startedAt,
            corpusSha256 = FrozenCorpusHash, manifestSha256 = HashFile(Path.Combine(root, CorpusDirectory, "corpus-manifest.json")),
            reviewLedgerSha256 = HashFile(Path.Combine(root, CorpusDirectory, "review-decisions.json")),
            evaluatorAssemblySha256 = HashFile(typeof(DeepSeekDevelopmentEvaluation).Assembly.Location),
            backendAssemblySha256 = HashFile(typeof(PmAnalyticsV3ReportPlan).Assembly.Location),
            schemaVersion = 3, catalogVersion = PmAnalyticsV3Catalog.Version,
            promptIdentity = "PmAnalyticsV3ModelPrompt", promptSha256 = HashText(PmAnalyticsV3ModelPrompt.SystemPrompt),
            model = client.ModelId, temperature = 0, thinking = "disabled", maximumOutputTokens = 1024,
            requestByteLimit = 32 * 1024, responseByteLimit = 16 * 1024, timeoutSeconds = 60,
            maximumHttpRequests = MaximumRequests, retries = 0, costCeilingUsd = BudgetUsd,
            inputUsdPerMillion = InputRate, outputUsdPerMillion = OutputRate, pricingCheckedDate = "2026-10-23",
            pricingSource = "https://api-docs.deepseek.com/quick_start/pricing/",
            costBasis = "Peak uncached input and peak output upper estimate; provider invoice not verified",
            interpretationAsOfUtc = now, database = "Disposable EF InMemory catalog only",
            catalogFixture = "One fire-extinguisher asset in fictional North Block; Research department absent",
            authorization = "User approved this 18-case Development-only DeepSeek comparison in chat on 2026-10-23",
            condition = "Explicit DeepSeek selection; actual interpreter including guard, constraints, validation and catalog resolution",
            comparison = "Unchanged frozen expectations and original ComparePartial; raw model candidate scored separately"
        };
        await WriteJson(Path.Combine(outputDirectory, "identity.json"), identity);
        var outcomes = new List<CaseResult>();
        foreach (var line in File.ReadLines(corpusPath))
        {
            using var itemDocument = JsonDocument.Parse(line);
            var item = itemDocument.RootElement;
            handler.BeginCase();
            var before = handler.Count;
            var stopwatch = Stopwatch.StartNew();
            PmAnalyticsV3InterpretationResponse? actual = null;
            string? error = null;
            if (ledger.Snapshot().TerminalFailureCode is not null || handler.BudgetStopped)
                error = "NotExecutedAfterTerminalFailure";
            else
            {
                try
                {
                    actual = await interpreter.InterpretAsync(item.GetProperty("question").GetString()!,
                        CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
                }
                catch (NaturalLanguageAnalyticsProviderException exception)
                {
                    error = exception.Failure + (exception.DiagnosticCode is null ? "" : ":" + exception.DiagnosticCode);
                    if (exception.Failure != NaturalLanguageAnalyticsProviderFailure.InvalidOutput)
                        ledger.MarkTerminalFailure(exception.DiagnosticCode ?? exception.Failure.ToString());
                }
            }
            stopwatch.Stop();
            // The candidate is parsed in memory, never saved as a raw provider payload.
            var candidate = handler.Candidate;
            var candidateScore = candidate is null ? null : Compare(item, candidate.Status, candidate.ClarificationFields, candidate.Plan);
            var actualScore = Compare(item, actual?.Status, actual?.ClarificationFields ?? [], actual?.Plan);
            if (error is not null) actualScore.Mismatches.Add("execution:" + error);
            outcomes.Add(new CaseResult(item.GetProperty("caseId").GetString()!, item.GetProperty("question").GetString()!,
                item.GetProperty("caseClass").GetString()!, item.GetProperty("expectedStatus").GetString()!,
                item.GetProperty("expectedClarificationFields").Deserialize<string[]>()!,
                item.GetProperty("expectedPlanDisposition").GetString()!, item.GetProperty("expectedPlanAssertions").Clone(),
                handler.Count - before, Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2), error,
                actual, actualScore, candidate, candidateScore));
            await WriteJson(Path.Combine(outputDirectory, "cases.json"), outcomes);
            Console.WriteLine($"{outcomes[^1].CaseId}: {actual?.Status ?? error}; requests={handler.Count - before}; mismatches={actualScore.Mismatches.Count}");
        }
        var snapshot = ledger.Snapshot();
        var completeUsage = snapshot.Calls.Where(call => call.HttpSucceeded).All(call =>
            call.Usage?.PromptTokens is not null && call.Usage.CompletionTokens is not null)
            && snapshot.Calls.All(call => call.HttpSucceeded);
        var promptTokens = snapshot.Calls.Sum(call => call.Usage?.PromptTokens ?? 0);
        var outputTokens = snapshot.Calls.Sum(call => call.Usage?.CompletionTokens ?? 0);
        var report = new
        {
            identity, completedAtUtc = DateTimeOffset.UtcNow,
            totalCases = outcomes.Count, attemptedCases = outcomes.Count(item => item.Error != "NotExecutedAfterTerminalFailure"),
            casesWithProviderRequest = outcomes.Count(item => item.ProviderRequests > 0), providerHttpRequests = handler.Count,
            preProviderSafetyCases = outcomes.Count(item => item.Actual?.Interpreter == "SafetyRules" && item.ProviderRequests == 0),
            statusMatches = outcomes.Count(item => item.Comparison.StatusMatch),
            clarificationSetMatches = outcomes.Count(item => item.Comparison.ClarificationMatch),
            dispositionMatches = outcomes.Count(item => item.Comparison.DispositionMatch),
            planAssertionMatches = outcomes.Sum(item => item.Comparison.MatchedAssertions),
            planAssertions = outcomes.Sum(item => item.Comparison.Assertions),
            exactCases = outcomes.Count(item => item.Comparison.Mismatches.Count == 0),
            unexpectedExecutableCases = outcomes.Count(item => item.ExpectedStatus != "Valid" && item.Actual?.Status == "Valid" && item.Actual.Plan is not null),
            unsupportedRejectionMatches = outcomes.Count(item => item.ExpectedStatus == "Unsupported" && item.Actual?.Status == "Unsupported" && item.Actual.Plan is null),
            rawModelCandidateCases = outcomes.Count(item => item.ModelCandidate is not null),
            rawModelCandidateExactCases = outcomes.Count(item => item.ModelCandidateComparison?.Mismatches.Count == 0),
            rawModelStatusMatches = outcomes.Count(item => item.ModelCandidateComparison?.StatusMatch == true),
            promptTokens, outputTokens, completeUsage,
            estimatedCostUsdAtPeakUncachedRates = completeUsage ? (decimal?)((promptTokens * InputRate + outputTokens * OutputRate) / 1_000_000m) : null,
            maximumReservedCostUsd = handler.ReservedCostUsd,
            provider = snapshot, cases = outcomes,
            limits = "Fixed researcher-reviewed development set; two labels revised before freeze. No held-out, SQL Server, report execution, browser, respondent or historical-session verification. Strict original comparisons include representation differences. Backend safety rejections are not model responses."
        };
        await WriteJson(Path.Combine(outputDirectory, "report.json"), report);
        Console.WriteLine($"Completed {outcomes.Count} cases; exact={report.exactCases}/18; assertions={report.planAssertionMatches}/{report.planAssertions}; provider requests={handler.Count}.");
        return outcomes.Any(item => item.Error is not null) ? 1 : 0;
    }

    private static Comparison Compare(JsonElement expected, string? status, IReadOnlyList<string> fields, PmAnalyticsV3ReportPlan? plan)
    {
        var statusMatch = expected.GetProperty("expectedStatus").GetString() == status;
        var clarificationMatch = expected.GetProperty("expectedClarificationFields").Deserialize<string[]>()!
            .Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var disposition = status == "Valid" && plan is not null ? "Executable"
            : status == "NeedsClarification" && plan is not null ? "ClarificationDraft" : "None";
        var dispositionMatch = expected.GetProperty("expectedPlanDisposition").GetString() == disposition;
        var mismatches = new List<string>();
        if (!statusMatch) mismatches.Add("status");
        if (!clarificationMatch) mismatches.Add("clarificationFields");
        if (!dispositionMatch) mismatches.Add("planDisposition");
        var count = 0;
        var matches = 0;
        var assertions = expected.GetProperty("expectedPlanAssertions");
        if (assertions.ValueKind == JsonValueKind.Object)
        {
            var serializedPlan = JsonSerializer.SerializeToElement(plan, JsonOptions);
            V3EvaluationHarness.ComparePartial(assertions, serializedPlan, "plan", mismatches, ref count, ref matches);
        }
        return new Comparison(statusMatch, clarificationMatch, dispositionMatch, count, matches, mismatches);
    }

    private static Task WriteJson(string path, object value) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions));
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Git identity check failed.");
        return output;
    }

    private sealed record Comparison(bool StatusMatch, bool ClarificationMatch, bool DispositionMatch,
        int Assertions, int MatchedAssertions, List<string> Mismatches);
    private sealed record CaseResult(string CaseId, string Question, string CaseClass, string ExpectedStatus,
        string[] ExpectedClarificationFields, string ExpectedPlanDisposition, JsonElement ExpectedPlanAssertions,
        int ProviderRequests, double ElapsedMilliseconds, string? Error, PmAnalyticsV3InterpretationResponse? Actual,
        Comparison Comparison, PmAnalyticsV3ModelOutput? ModelCandidate, Comparison? ModelCandidateComparison);

    private sealed class SingleAttemptHandler(string directory) : DelegatingHandler(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false
    })
    {
        internal int Count { get; private set; }
        internal decimal ReservedCostUsd { get; private set; }
        internal bool BudgetStopped { get; private set; }
        internal PmAnalyticsV3ModelOutput? Candidate { get; private set; }
        private bool _caseAttempted;
        internal void BeginCase() { _caseAttempted = false; Candidate = null; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (_caseAttempted || Count >= MaximumRequests)
                throw new NaturalLanguageAnalyticsProviderException(NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable, "EvaluationRetryBlocked");
            _caseAttempted = true;
            var requestBytes = await request.Content!.ReadAsByteArrayAsync(token);
            // One token per request byte plus 4096 framing tokens is a conservative reservation
            // for this fixed ASCII corpus and prompt. Reserve full output even on failure.
            var reservation = ((requestBytes.Length + 4096m) * InputRate + 1024m * OutputRate) / 1_000_000m;
            if (ReservedCostUsd + reservation > BudgetUsd)
            {
                BudgetStopped = true;
                throw new NaturalLanguageAnalyticsProviderException(NaturalLanguageAnalyticsProviderFailure.ProviderUnavailable, "EvaluationCostCeiling");
            }
            ReservedCostUsd += reservation;
            Count++;
            await File.WriteAllTextAsync(Path.Combine(directory, "request-count.txt"), Count.ToString(CultureInfo.InvariantCulture), token);
            var response = await base.SendAsync(request, token);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await NaturalLanguageAnalyticsApiModelClientBase.ReadBoundedAsync(response.Content, 16 * 1024, token);
                var oldContent = response.Content;
                response.Content = new ByteArrayContent(bytes);
                oldContent.Dispose();
                try
                {
                    using var document = JsonDocument.Parse(bytes);
                    var text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                    if (text is not null) Candidate = PmAnalyticsV3ModelOutputParser.Parse(text);
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or NaturalLanguageAnalyticsProviderException)
                {
                    // Let the unchanged client and interpreter classify invalid output.
                }
            }
            return response;
        }
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private sealed class MemoryFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
    private sealed class EvaluationOptions : IOptionsMonitor<NaturalLanguageAnalyticsOptions>
    {
        public NaturalLanguageAnalyticsOptions CurrentValue { get; } = new() { V3DeepSeekEnabled = true };
        public NaturalLanguageAnalyticsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NaturalLanguageAnalyticsOptions, string?> listener) => null;
    }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UniPM.V3DevelopmentEvaluation";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
