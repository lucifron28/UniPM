using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features;
using UniPM.Api.Health;
using UniPM.Api.Features.Retrieval;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ILexicalMaintenanceRetriever, SqlServerLexicalMaintenanceRetriever>();
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.AddHttpClient<IEmbeddingService, OpenAiCompatibleEmbeddingService>();
builder.Services.AddScoped<IMaintenanceSearchDocumentEmbeddingIndexer, MaintenanceSearchDocumentEmbeddingIndexer>();
builder.Services.AddScoped<ISemanticMaintenanceRetriever, SqlServerSemanticMaintenanceRetriever>();

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
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

public partial class Program;
