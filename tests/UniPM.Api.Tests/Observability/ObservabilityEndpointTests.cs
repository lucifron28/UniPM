using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniPM.Api.Observability;

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

    [Fact]
    public async Task Metrics_exports_custom_retrieval_families_with_bounded_labels()
    {
        await using var application = new MetricsEnabledApplicationFactory();
        using var client = application.CreateClient();
        var metrics = application.Services.GetRequiredService<UniPMMetrics>();

        metrics.RecordRetrieval("lexical", "success", 3, 0.25);
        metrics.RecordRetrieval("fused", "degraded", 1, 0.5);

        var response = await client.GetAsync("/metrics");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            "(?m)^unipm_retrieval_requests_total\\{[^}]*channel=\"lexical\",outcome=\"success\"\\} 1$",
            content);
        Assert.Matches(
            "(?m)^unipm_retrieval_requests_total\\{[^}]*channel=\"fused\",outcome=\"degraded\"\\} 1$",
            content);
        Assert.Matches(
            "(?m)^unipm_retrieval_duration_seconds_bucket\\{[^}]*channel=\"lexical\",le=\"0.25\"\\} 1$",
            content);
        Assert.Contains(
            "unipm_retrieval_results_sum{otel_scope_name=\"UniPM.Api\",otel_scope_version=\"1.0.0\",channel=\"lexical\"} 3",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("unipm_embedding", content, StringComparison.Ordinal);
        Assert.DoesNotContain("unipm_search_projection", content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-maintenance-query", content, StringComparison.Ordinal);
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
