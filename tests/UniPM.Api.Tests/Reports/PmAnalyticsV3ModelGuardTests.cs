using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.Reports;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3ModelGuardTests
{
    [Fact]
    public async Task Model_ranking_and_catalog_clarification_follow_the_question_contract()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("ModelGuards-" + Guid.NewGuid()).Options;
        var clock = new FrozenClock();
        var query = new PmAnalyticsV3QueryService(new MemoryFactory(options), clock);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DEEPSEEK_API_KEY"] = "synthetic-test-only" }).Build();
        using var interpreter = new PmAnalyticsV3QuestionInterpreter(
            new EnabledOptions(), new DevelopmentEnvironment(), clock, query, configuration);
        using var handler = new CandidateHandler();
        var field = typeof(PmAnalyticsV3QuestionInterpreter).GetField("_modelClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((IDisposable)field.GetValue(interpreter)!).Dispose();
        field.SetValue(interpreter, new DeepSeekNaturalLanguageAnalyticsModelClient(
            "synthetic-test-only", new NaturalLanguageAnalyticsModelClientRunLedger(), handler,
            promptProfile: PmAnalyticsV3ModelPrompt.Profile));

        var plan = new PmAnalyticsV3ReportPlan(
            3, PmAnalyticsV3Catalog.Version, PmAnalyticsV3Catalog.SubjectPmExecution,
            PmAnalyticsV3Mode.Aggregate, new PmAnalyticsV3Period("pmCycle", PmAnalyticsV3PeriodKind.Year, 2024),
            ["fire-extinguisher"], ["scheduledCount", "completedCount"], ["department"], [], [], [], [],
            new PmAnalyticsV3Top("scheduledCount", 5, PmAnalyticsV3SortDirection.Descending),
            PmAnalyticsV3Presentation.Table, ["department", "scheduledCount", "completedCount"]);

        foreach (var question in new[]
        {
            "Show scheduled and completed fire extinguisher counts for the top 5 departments in 2024",
            "Show completed and scheduled fire extinguisher counts for the bottom 3 departments in 2024"
        })
        {
            handler.Plan = plan;
            var actual = await Interpret(question);
            Assert.Equal("NeedsClarification", actual.Status);
            Assert.Contains("Top", actual.ClarificationFields);
            Assert.NotNull(actual.Plan);
            Assert.Null(actual.Plan.Top);
            Assert.Equal(plan.Measures, actual.Plan.Measures);
        }

        // A model cannot make ambiguous wording safe by dropping a requested measure.
        handler.Plan = plan with { Measures = ["scheduledCount"], Columns = ["department", "scheduledCount"] };
        var dropped = await Interpret("Show scheduled and completed fire extinguisher counts for the top 5 departments in 2024");
        Assert.Equal("NeedsClarification", dropped.Status);
        Assert.Contains("Top", dropped.ClarificationFields);
        Assert.Equal(plan.Measures, dropped.Plan!.Measures);

        handler.Plan = plan with { Top = new PmAnalyticsV3Top("completedCount", 5, PmAnalyticsV3SortDirection.Descending) };
        const string explicitRanking = "Show scheduled and completed fire extinguisher counts for the top 5 departments by completed count in 2024";
        var explicitResult = await Interpret(explicitRanking);
        Assert.Equal("Valid", explicitResult.Status);
        Assert.Equal("completedCount", explicitResult.Plan!.Top!.Measure);

        handler.Plan = plan with { Measures = ["completedCount"], Columns = ["department", "completedCount"],
            Top = new PmAnalyticsV3Top("completedCount", 5, PmAnalyticsV3SortDirection.Descending) };
        var single = await Interpret("Show completed fire extinguisher counts for the top 5 departments in 2024");
        Assert.Equal("Valid", single.Status);

        handler.Plan = plan;
        var wrongExplicit = await Interpret(explicitRanking);
        Assert.Equal("NeedsClarification", wrongExplicit.Status);
        Assert.Contains("Top", wrongExplicit.ClarificationFields);
        Assert.Null(wrongExplicit.Plan!.Top);

        handler.Plan = plan with { Mode = PmAnalyticsV3Mode.Records, Measures = [], GroupBy = [], Top = null, Columns = ["assetCode"] };
        var records = await Interpret("Show scheduled and completed fire extinguisher counts for the top 5 departments in 2024");
        Assert.Equal("NeedsClarification", records.Status);
        Assert.Contains("Mode", records.ClarificationFields);
        Assert.Contains("Top", records.ClarificationFields);

        foreach (var (filter, clarification, value) in new[]
        {
            ("department", "Department", "Absent Department"),
            ("building", "Building", "Absent Building"),
            ("location", "Location", "Absent Location"),
            ("assetCode", "AssetCode", "ABSENT-001"),
            ("assetId", "AssetId", "00000000-0000-0000-0000-000000000099")
        })
        {
            handler.Plan = plan with { Top = null,
                BaseFilters = [new PmAnalyticsV3Filter(filter, PmAnalyticsV3FilterOperator.Equal, [value])] };
            var actual = await Interpret("Show scheduled and completed fire extinguisher counts in 2024");
            Assert.Equal("NeedsClarification", actual.Status);
            Assert.Equal("UnknownCatalogValue", actual.Code);
            Assert.Equal([clarification], actual.ClarificationFields);
            Assert.Contains(actual.ClarificationFields[0], PmAnalyticsV3Catalog.ClarificationFields);
            Assert.NotNull(actual.Plan);
            Assert.Equal(value, actual.Plan.BaseFilters.Single().Values!.Single());
        }

        Assert.Equal(12, handler.Calls);
        Task<PmAnalyticsV3InterpretationResponse> Interpret(string question) => interpreter.InterpretAsync(
            question, CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
    }

    private sealed class CandidateHandler : HttpMessageHandler
    {
        internal PmAnalyticsV3ReportPlan Plan { get; set; } = null!;
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var candidate = JsonSerializer.Serialize(new PmAnalyticsV3ModelOutput("Valid", Plan, [], null, null), jsonOptions);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = "synthetic-test-only", choices = new[] { new { message = new { content = candidate } } },
                    usage = new { prompt_tokens = 0, completion_tokens = 0, prompt_cache_hit_tokens = 0, prompt_cache_miss_tokens = 0 }
                }, jsonOptions))
            });
        }
    }
    private sealed class FrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    }
    private sealed class MemoryFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
    private sealed class EnabledOptions : IOptionsMonitor<NaturalLanguageAnalyticsOptions>
    {
        public NaturalLanguageAnalyticsOptions CurrentValue { get; } = new() { V3DeepSeekEnabled = true };
        public NaturalLanguageAnalyticsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NaturalLanguageAnalyticsOptions, string?> listener) => null;
    }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UniPM.ModelGuardTests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
