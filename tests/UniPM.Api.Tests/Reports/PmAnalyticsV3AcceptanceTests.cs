using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Reports;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Tests;

public sealed class PmAnalyticsV3AcceptanceTests
{
    [Fact]
    public async Task Former_exact_templates_use_the_deterministic_parser_with_explicit_provenance()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        const string exactQuestion = "SHOW LATE FIRE-EXTINGUISHER INSPECTIONS FOR THE 2026 PM CYCLES.";
        const string variedQuestion = "   show   late   fire-extinguisher   inspections   for  the   2026  pm   cycles.   ";
        const string missedByDepartmentQuestion =
            "SHOW MISSED FIRE-EXTINGUISHER INSPECTIONS BY DEPARTMENT FOR THE 2026 PM CYCLES.";

        using var exactResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(exactQuestion));
        Assert.Equal(HttpStatusCode.OK, exactResponse.StatusCode);
        var exactInterpretation = await exactResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(exactInterpretation);
        Assert.Equal("Valid", exactInterpretation.Status);
        Assert.Equal("DeterministicParser", exactInterpretation.Interpreter);
        Assert.NotNull(exactInterpretation.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, exactInterpretation.Plan.Subject);
        Assert.Equal(["fire-extinguisher"], exactInterpretation.Plan.Categories);
        Assert.NotNull(exactInterpretation.Plan.Period);
        Assert.Equal(2026, exactInterpretation.Plan.Period.Year);
        Assert.Equal([PmAnalyticsV3Catalog.CompletedLateCount], exactInterpretation.Plan.Measures);

        using var variedResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(variedQuestion));
        Assert.Equal(HttpStatusCode.OK, variedResponse.StatusCode);
        var variedInterpretation = await variedResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(variedInterpretation);
        Assert.Equal("Valid", variedInterpretation.Status);
        Assert.Equal("DeterministicParser", variedInterpretation.Interpreter);
        Assert.NotNull(variedInterpretation.Plan);
        Assert.Equal(["fire-extinguisher"], variedInterpretation.Plan.Categories);
        Assert.NotNull(variedInterpretation.Plan.Period);
        Assert.Equal(2026, variedInterpretation.Plan.Period.Year);
        Assert.Equal([PmAnalyticsV3Catalog.CompletedLateCount], variedInterpretation.Plan.Measures);

        using var missedResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(missedByDepartmentQuestion));
        Assert.Equal(HttpStatusCode.OK, missedResponse.StatusCode);
        var missedInterpretation = await missedResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(missedInterpretation);
        Assert.Equal("Valid", missedInterpretation.Status);
        Assert.Equal("DeterministicParser", missedInterpretation.Interpreter);
        Assert.NotNull(missedInterpretation.Plan);
        Assert.Equal(PmAnalyticsV3Catalog.SubjectPmExecution, missedInterpretation.Plan.Subject);
        Assert.Equal(["fire-extinguisher"], missedInterpretation.Plan.Categories);
        Assert.Equal([PmAnalyticsV3Catalog.MissedCount], missedInterpretation.Plan.Measures);
        Assert.Equal([PmAnalyticsV3Catalog.Department], missedInterpretation.Plan.GroupBy);
        Assert.Equal(PmAnalyticsV3Presentation.Table, missedInterpretation.Plan.Presentation);
    }

    [Fact]
    public async Task Changed_questions_use_the_parser_when_disabled_and_provider_failure_does_not_fall_back()
    {
        // With DeepSeek disabled, changed wording is interpreted by the deterministic parser.
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        const string exactQuestion = "SHOW LATE FIRE-EXTINGUISHER INSPECTIONS FOR THE 2026 PM CYCLES.";
        const string changedYear = "SHOW LATE FIRE-EXTINGUISHER INSPECTIONS FOR THE 2025 PM CYCLES.";
        const string changedWords = "SHOW LATE FIRE-EXTINGUISHER CHECKS FOR THE 2026 PM CYCLES.";

        using var yearResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(changedYear));
        Assert.Equal(HttpStatusCode.OK, yearResponse.StatusCode);
        var yearInterpretation = await yearResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(yearInterpretation);
        Assert.Equal("DeterministicParser", yearInterpretation.Interpreter);

        using var wordsResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(changedWords));
        Assert.Equal(HttpStatusCode.OK, wordsResponse.StatusCode);
        var wordsInterpretation = await wordsResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(wordsInterpretation);
        Assert.Equal("DeterministicParser", wordsInterpretation.Interpreter);

        // A former exact template is not exempt from the configured provider path.
        await using var deepSeekApp = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: true,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var deepSeekClient = deepSeekApp.CreateClient();

        using var explicitParserResponse = await deepSeekClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(
                exactQuestion,
                PmAnalyticsV3Interpreter.DeterministicParser));
        Assert.Equal(HttpStatusCode.OK, explicitParserResponse.StatusCode);
        var explicitParserInterpretation = await explicitParserResponse.Content
            .ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(explicitParserInterpretation);
        Assert.Equal("Valid", explicitParserInterpretation.Status);
        Assert.Equal("DeterministicParser", explicitParserInterpretation.Interpreter);

        using var providerFailResponse = await deepSeekClient.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest(exactQuestion));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, providerFailResponse.StatusCode);
        using var providerFailure = JsonDocument.Parse(await providerFailResponse.Content.ReadAsStringAsync());
        Assert.Equal("ProviderUnavailable", providerFailure.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Scenario_3_Missing_period_returns_needs_clarification_with_period_and_retains_category()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Which fire extinguishers were completed late?"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var interpretation = await response.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation);
        Assert.Equal("NeedsClarification", interpretation.Status);
        Assert.Contains("Period", interpretation.ClarificationFields);
        Assert.NotNull(interpretation.Plan);
        Assert.Contains("fire-extinguisher", interpretation.Plan.Categories);
        Assert.Contains(PmAnalyticsV3Catalog.CompletedLateCount, interpretation.Plan.Measures);
    }

    [Fact]
    public async Task Scenario_4_Ambiguous_behind_wording_returns_needs_clarification_with_period_and_measures_and_strips_measures()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Which departments are falling behind on extinguisher checks?"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var interpretation = await response.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation);
        Assert.Equal("NeedsClarification", interpretation.Status);
        Assert.Contains("Period", interpretation.ClarificationFields);
        Assert.Contains("Measures", interpretation.ClarificationFields);
        Assert.NotNull(interpretation.Plan);
        Assert.Contains("fire-extinguisher", interpretation.Plan.Categories);
        Assert.NotNull(interpretation.Plan.GroupBy);
        Assert.Contains(PmAnalyticsV3Catalog.Department, interpretation.Plan.GroupBy);
        Assert.Empty(interpretation.Plan.Measures);
    }

    [Fact]
    public async Task Scenario_5_Non_operational_findings_matches_condition_filter_or_non_operational_count()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        // Records mode: matches Condition == NonOperational
        using var recordsResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show non-operational fire extinguisher records for 2026"));
        Assert.Equal(HttpStatusCode.OK, recordsResponse.StatusCode);
        var recordsInterpretation = await recordsResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(recordsInterpretation);
        Assert.Equal("Valid", recordsInterpretation.Status);
        Assert.NotNull(recordsInterpretation.Plan);
        Assert.Equal(PmAnalyticsV3Mode.Records, recordsInterpretation.Plan.Mode);
        Assert.Contains(recordsInterpretation.Plan.MatchingFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Condition
            && filter.Operator == PmAnalyticsV3FilterOperator.Equal
            && filter.Values?.Contains("NonOperational", StringComparer.Ordinal) == true);

        // Aggregate mode: matches nonOperationalCount measure
        using var aggregateResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show non-operational fire extinguisher findings for 2026"));
        Assert.Equal(HttpStatusCode.OK, aggregateResponse.StatusCode);
        var aggregateInterpretation = await aggregateResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(aggregateInterpretation);
        Assert.Equal("Valid", aggregateInterpretation.Status);
        Assert.NotNull(aggregateInterpretation.Plan);
        Assert.Equal(PmAnalyticsV3Mode.Aggregate, aggregateInterpretation.Plan.Mode);
        Assert.Contains(PmAnalyticsV3Catalog.NonOperationalCount, aggregateInterpretation.Plan.Measures);
    }

    [Fact]
    public async Task Scenario_6_Inspection_date_vs_pm_cycle_returns_period_basis_needs_clarification()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show extinguisher inspections on June 2026"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var interpretation = await response.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation);
        Assert.Equal("NeedsClarification", interpretation.Status);
        Assert.Equal("PeriodBasisNeedsClarification", interpretation.Code);
        Assert.Contains("Period", interpretation.ClarificationFields);
        Assert.Null(interpretation.Plan);
    }

    [Fact]
    public async Task Scenario_7_Percentage_threshold_resolves_aggregate_filter_with_less_than()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show fire extinguisher on-time compliance by department below 80% for 2026"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var interpretation = await response.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation);
        Assert.Equal("Valid", interpretation.Status);
        Assert.NotNull(interpretation.Plan);
        Assert.Contains(PmAnalyticsV3Catalog.OnTimeCompliancePercent, interpretation.Plan.Measures);
        Assert.NotNull(interpretation.Plan.GroupBy);
        Assert.Contains(PmAnalyticsV3Catalog.Department, interpretation.Plan.GroupBy);
        Assert.NotNull(interpretation.Plan.AggregateFilters);
        var threshold = Assert.Single(interpretation.Plan.AggregateFilters);
        Assert.Equal(PmAnalyticsV3Catalog.OnTimeCompliancePercent, threshold.Measure);
        Assert.Equal(PmAnalyticsV3NumericOperator.LessThan, threshold.Operator);
        Assert.Equal(80m, threshold.Value);
    }

    [Fact]
    public async Task Scenario_8_Named_department_and_building_resolves_catalog_filters()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();
        await SeedCatalogAssetsAsync(application.Services);

        // Quoted building filter
        using var buildingResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show completed fire extinguishers for building 'North Block' during 2026"));
        Assert.Equal(HttpStatusCode.OK, buildingResponse.StatusCode);
        var buildingInterpretation = await buildingResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(buildingInterpretation);
        Assert.Equal("Valid", buildingInterpretation.Status);
        Assert.NotNull(buildingInterpretation.Plan);
        Assert.Contains(buildingInterpretation.Plan.BaseFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Building
            && filter.Operator == PmAnalyticsV3FilterOperator.Equal
            && filter.Values?.Contains("North Block", StringComparer.OrdinalIgnoreCase) == true);

        // Quoted department filter
        using var departmentResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show completed fire extinguishers for department 'Chemistry' during 2026"));
        Assert.Equal(HttpStatusCode.OK, departmentResponse.StatusCode);
        var departmentInterpretation = await departmentResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(departmentInterpretation);
        Assert.Equal("Valid", departmentInterpretation.Status);
        Assert.NotNull(departmentInterpretation.Plan);
        Assert.Contains(departmentInterpretation.Plan.BaseFilters, filter =>
            filter.Field == PmAnalyticsV3Catalog.Department
            && filter.Operator == PmAnalyticsV3FilterOperator.Equal
            && filter.Values?.Contains("Chemistry", StringComparer.OrdinalIgnoreCase) == true);

        // Unquoted department mention prompts clarification
        using var unquotedResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show fire extinguisher compliance for the Research department in 2026"));
        Assert.Equal(HttpStatusCode.OK, unquotedResponse.StatusCode);
        var unquotedInterpretation = await unquotedResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(unquotedInterpretation);
        Assert.Equal("NeedsClarification", unquotedInterpretation.Status);
        Assert.Contains("Department", unquotedInterpretation.ClarificationFields);

        // Unknown building prompts clarification with UnknownCatalogValue
        using var unknownResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show completed fire extinguishers for building 'Nonexistent Tower' during 2026"));
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        var unknownInterpretation = await unknownResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(unknownInterpretation);
        Assert.Equal("NeedsClarification", unknownInterpretation.Status);
        Assert.Equal("UnknownCatalogValue", unknownInterpretation.Code);
        Assert.Contains("building", unknownInterpretation.ClarificationFields);
    }

    [Fact]
    public async Task Scenario_14_Confirmed_preview_executes_against_query_service_and_returns_population_and_rows()
    {
        await using var application = new AcceptanceApplicationFactory(
            authenticated: true,
            deepSeekEnabled: false,
            apiKey: null,
            roles: [AuthRoleCatalog.Gsd]);
        using var client = application.CreateClient();

        await SeedPmExecutionDataAsync(application.Services);

        // 1. Interpret valid question
        using var interpretResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/interpret",
            new PmAnalyticsV3InterpretRequest("Show completed fire extinguishers during 2024"));
        Assert.Equal(HttpStatusCode.OK, interpretResponse.StatusCode);
        var interpretation = await interpretResponse.Content.ReadFromJsonAsync<PmAnalyticsV3InterpretationResponse>();
        Assert.NotNull(interpretation?.Plan);

        // 2. Generate preview
        using var previewResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/preview",
            new PmAnalyticsV3PreviewRequest(interpretation.Plan));
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<PmAnalyticsV3PreviewResponse>();
        Assert.NotNull(preview);
        Assert.False(string.IsNullOrWhiteSpace(preview.PreviewId));

        // 3. Confirm and execute preview
        using var executeResponse = await client.PostAsJsonAsync(
            "/api/v1/analytics/pm/v3/execute",
            new PmAnalyticsV3ExecuteRequest(preview.PreviewId));
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        var execution = await executeResponse.Content.ReadFromJsonAsync<PmAnalyticsV3ExecutionResponse>();
        Assert.NotNull(execution);
        Assert.Equal(1, execution.Population.EligibleAssetCycleCount);
        Assert.NotNull(execution.AggregateRows);
        Assert.Single(execution.AggregateRows);
        var row = execution.AggregateRows[0];
        Assert.True(row.Measures.ContainsKey(PmAnalyticsV3Catalog.CompletedCount));
        Assert.Equal(1, row.Measures[PmAnalyticsV3Catalog.CompletedCount].Value);
    }

    private static async Task SeedPmExecutionDataAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        var assetId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var cycle = "2024-02";

        var asset = new Asset
        {
            Id = assetId,
            AssetCode = "FE-ACCEPT-01",
            AssetCategory = "fire-extinguisher",
            Department = "Demo Department",
            Building = "Demo Building",
            Location = "Demo Location",
            Status = "Active"
        };
        context.Assets.Add(asset);

        var schedule = new PreventiveMaintenanceSchedule
        {
            Id = scheduleId,
            AssetId = assetId,
            Asset = asset,
            PmCycle = cycle,
            ScheduleDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)),
            PeriodType = "Quarter",
            Status = ScheduleStatusCatalog.Completed
        };
        context.PreventiveMaintenanceSchedules.Add(schedule);

        var inspectorId = Guid.NewGuid();
        var inspection = new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Schedule = schedule,
            AssetId = assetId,
            Asset = asset,
            InspectorUserId = inspectorId,
            DateInspected = new DateTimeOffset(2024, 2, 15, 10, 0, 0, TimeSpan.FromHours(8)),
            CompletedAt = new DateTimeOffset(2024, 2, 15, 12, 0, 0, TimeSpan.FromHours(8)),
            IsOperational = true
        };
        context.InspectionRecords.Add(inspection);

        await context.SaveChangesAsync();
    }

    private static async Task SeedCatalogAssetsAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        context.Assets.AddRange(
            new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "FE-CAT-01",
                AssetCategory = "fire-extinguisher",
                Department = "Chemistry",
                Building = "North Block",
                Location = "Room 101",
                Status = "Active"
            },
            new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = "FE-CAT-02",
                AssetCategory = "fire-extinguisher",
                Department = "Research",
                Building = "Main Building",
                Location = "Room 202",
                Status = "Active"
            });
        await context.SaveChangesAsync();
    }

    private sealed class AcceptanceApplicationFactory(
        bool authenticated,
        bool deepSeekEnabled,
        string? apiKey,
        string[] roles) : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"PmAnalyticsV3Acceptance_{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false",
                    ["NaturalLanguageAnalytics:V3DeepSeekEnabled"] = deepSeekEnabled ? "true" : "false",
                    [DeepSeekNaturalLanguageAnalyticsModelClient.ApiKeyEnvironmentVariable] = apiKey
                }));
            builder.ConfigureServices(services =>
            {
                if (authenticated) services.AddTestAuthentication(roles);
                services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContextFactory<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        }
    }
}
