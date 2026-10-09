using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Tests;

internal static class TestApplicationBuilderExtensions
{
    public static IWebHostBuilder DisableScheduleGenerationWorker(this IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false"
            }));
        builder.ConfigureServices(services =>
        {
            var workers = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(PreventiveMaintenanceScheduleGenerationWorker))
                .ToArray();
            foreach (var worker in workers)
            {
                services.Remove(worker);
            }
        });

        return builder;
    }
}
