using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features;
using UniPM.Api.Health;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Observability;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var metricsEnabled = builder.Configuration.GetValue<bool>(
    $"{ObservabilityOptions.SectionName}:MetricsEnabled");

builder.Services.Configure<ObservabilityOptions>(
    builder.Configuration.GetSection(ObservabilityOptions.SectionName));
builder.Services.AddMetrics();
builder.Services.AddSingleton<UniPMMetrics>();
if (metricsEnabled)
{
    builder.Services
        .AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddMeter(UniPMMetrics.MeterName)
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddMeter("System.Runtime")
            .AddView(
                "http.server.request.duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                })
            .AddView(
                "unipm.retrieval.duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5]
                })
            .AddPrometheusExporter(exporter =>
            {
                exporter.ScrapeEndpointPath = "/metrics";
                exporter.ScrapeResponseCacheDurationMilliseconds = 0;
            }));
}

builder.Services.AddOpenApi();
builder.Services.AddDbContextFactory<ApplicationDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddSingleton<SyntheticMaintenanceSeedOptions>();
builder.Services.AddSingleton<SyntheticMaintenanceDatasetValidator>();
builder.Services.AddSingleton<SyntheticMaintenanceDatasetLoader>();
builder.Services.AddScoped<SyntheticMaintenanceSeeder>();
builder.Services.AddSingleton<MaintenanceIssueLexiconOptions>();
builder.Services.AddSingleton<MaintenanceIssueLexiconLoader>();
builder.Services.AddSingleton<MaintenanceIssueNormalizer>();
builder.Services.AddScoped<MaintenanceSearchDocumentProjector>();
builder.Services.AddScoped<SqlServerLexicalMaintenanceRetriever>();
builder.Services.AddScoped<ILexicalMaintenanceRetriever>(serviceProvider =>
    new MetricsLexicalMaintenanceRetriever(
        serviceProvider.GetRequiredService<SqlServerLexicalMaintenanceRetriever>(),
        serviceProvider.GetRequiredService<UniPMMetrics>()));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.AddHttpClient<IEmbeddingService, OpenAiCompatibleEmbeddingService>();
builder.Services.AddScoped<IMaintenanceSearchDocumentEmbeddingIndexer, MaintenanceSearchDocumentEmbeddingIndexer>();
builder.Services.AddScoped<SqlServerSemanticMaintenanceRetriever>();
builder.Services.AddScoped<ISemanticMaintenanceRetriever>(serviceProvider =>
    new MetricsSemanticMaintenanceRetriever(
        serviceProvider.GetRequiredService<SqlServerSemanticMaintenanceRetriever>(),
        serviceProvider.GetRequiredService<UniPMMetrics>()));
builder.Services.AddScoped<FusedMaintenanceRetriever>();
builder.Services.AddScoped<IFusedMaintenanceRetriever>(serviceProvider =>
    new MetricsFusedMaintenanceRetriever(
        serviceProvider.GetRequiredService<FusedMaintenanceRetriever>(),
        serviceProvider.GetRequiredService<UniPMMetrics>()));

var app = builder.Build();

var maintenanceCommand = SyntheticMaintenanceCommandParser.Parse(args);
if (maintenanceCommand != SyntheticMaintenanceCommand.None)
{
    if (maintenanceCommand == SyntheticMaintenanceCommand.Ambiguous)
    {
        await Console.Error.WriteLineAsync("Specify only one maintenance command.");
        Environment.ExitCode = 1;
        return;
    }

    if (maintenanceCommand is (SyntheticMaintenanceCommand.Seed or SyntheticMaintenanceCommand.Reset)
        && !app.Environment.IsDevelopment())
    {
        await Console.Error.WriteLineAsync("Synthetic maintenance seed commands are available only in Development.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        if (maintenanceCommand == SyntheticMaintenanceCommand.Rebuild)
        {
            var projector = scope.ServiceProvider.GetRequiredService<MaintenanceSearchDocumentProjector>();
            var result = await projector.RebuildAsync();
            await Console.Out.WriteLineAsync(
                $"Rebuilt {result.Total} maintenance search documents ({result.Created} created, {result.Updated} updated, {result.Removed} removed).");
        }
        else if (maintenanceCommand == SyntheticMaintenanceCommand.RebuildEmbeddings)
        {
            var indexer = scope.ServiceProvider
                .GetRequiredService<IMaintenanceSearchDocumentEmbeddingIndexer>();
            var result = await indexer.RebuildAsync();
            await Console.Out.WriteLineAsync(
                $"Rebuilt {result.Total} maintenance embeddings ({result.Created} created, {result.Updated} updated, {result.Skipped} skipped, {result.Failed} failed).");
            if (result.Failed > 0)
            {
                Environment.ExitCode = 1;
            }
        }
        else
        {
            var seeder = scope.ServiceProvider.GetRequiredService<SyntheticMaintenanceSeeder>();
            if (maintenanceCommand == SyntheticMaintenanceCommand.Seed)
            {
                var result = await seeder.SeedAsync();
                await Console.Out.WriteLineAsync($"Seeded {result.Assets} assets, {result.Schedules} schedules, and {result.Inspections} inspections.");
            }
            else
            {
                var result = await seeder.ResetAsync();
                await Console.Out.WriteLineAsync($"Removed {result.AssetsRemoved} assets, {result.SchedulesRemoved} schedules, and {result.InspectionsRemoved} inspections.");
            }
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Maintenance command failed.");
        Environment.ExitCode = 1;
    }

    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (metricsEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/metrics"
            && context.Features.Get<IHttpMetricsTagsFeature>() is { } metricsFeature)
        {
            metricsFeature.MetricsDisabled = true;
        }

        await next(context);
    });
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "UniPM API",
    status = "running"
}))
.WithName("GetApiInfo");

app.MapApiEndpoints();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).DisableHttpMetrics();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).DisableHttpMetrics();

app.Run();

public partial class Program;
