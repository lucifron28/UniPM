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

var app = builder.Build();

var seedCommand = SyntheticMaintenanceCommandParser.Parse(args);
if (seedCommand != SyntheticMaintenanceCommand.None)
{
    if (seedCommand == SyntheticMaintenanceCommand.Ambiguous)
    {
        await Console.Error.WriteLineAsync("Specify only one synthetic maintenance seed command.");
        Environment.ExitCode = 1;
        return;
    }

    if (!app.Environment.IsDevelopment())
    {
        await Console.Error.WriteLineAsync("Synthetic maintenance seed commands are available only in Development.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<SyntheticMaintenanceSeeder>();

        if (seedCommand == SyntheticMaintenanceCommand.Seed)
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
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Synthetic maintenance seed command failed.");
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
