using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace UniPM.Api.Tests.Observability;

public sealed class ObservabilityEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> defaultApplication;

    public ObservabilityEndpointTests(WebApplicationFactory<Program> defaultApplication)
    {
        this.defaultApplication = defaultApplication;
    }

    [Fact]
    public async Task Metrics_returns_not_found_when_disabled()
    {
        using var client = defaultApplication.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_returns_prometheus_content_and_excludes_health_and_metrics_routes_when_enabled()
    {
        await using var application = new MetricsEnabledApplicationFactory();
        using var client = application.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        await client.GetAsync("/health/live?query=private-maintenance-query");

        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/plain", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http_server_request_duration", content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-maintenance-query", content, StringComparison.Ordinal);
        Assert.DoesNotContain("/metrics", content, StringComparison.Ordinal);
        Assert.DoesNotContain("/health/live", content, StringComparison.Ordinal);
        Assert.DoesNotContain("connection-string", content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MetricsEnabledApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Observability:MetricsEnabled", "true");
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        }
    }
}
