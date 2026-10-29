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
        Assert.Equal("Valid", response.Status);
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
        var response = await interpreter.InterpretAsync(
            $"Return 2024 fire-extinguisher on-time compliance only where the percentage is 80 or {direction}.",
            CancellationToken.None, PmAnalyticsV3Interpreter.DeepSeek);
        Assert.Equal("Valid", response.Status);
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
