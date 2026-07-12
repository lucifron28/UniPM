using System.Text.Json;

namespace UniPM.Api.Tests.Observability;

public sealed class ObservabilityProvisioningTests
{
    [Fact]
    public void Prometheus_configuration_targets_only_the_api_metrics_endpoint()
    {
        var content = ReadRepositoryFile("observability/prometheus/prometheus.yml");

        Assert.Contains("job_name: unipm-api", content, StringComparison.Ordinal);
        Assert.Contains("unipm-api:8080", content, StringComparison.Ordinal);
        Assert.Contains("scrape_interval: 15s", content, StringComparison.Ordinal);
        Assert.DoesNotContain("health", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grafana_datasource_is_pinned_to_the_provisioned_prometheus_uid()
    {
        var content = ReadRepositoryFile(
            "observability/grafana/provisioning/datasources/prometheus.yml");

        Assert.Contains("uid: unipm-prometheus", content, StringComparison.Ordinal);
        Assert.Contains("url: http://prometheus:9090", content, StringComparison.Ordinal);
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dashboard_json_has_stable_identity_and_technical_panels()
    {
        using var document = JsonDocument.Parse(ReadRepositoryFile(
            "observability/grafana/dashboards/unipm-system-health.json"));
        var root = document.RootElement;
        var titles = root.GetProperty("panels")
            .EnumerateArray()
            .Select(panel => panel.GetProperty("title").GetString())
            .ToArray();

        Assert.Equal("unipm-system-health", root.GetProperty("uid").GetString());
        Assert.Equal("UniPM API System Health", root.GetProperty("title").GetString());
        Assert.Contains("Prometheus target status", titles);
        Assert.Contains("Retrieval requests by channel and outcome", titles);
        var dashboardJson = root.GetRawText();
        Assert.Contains("dotnet_process_memory_working_set_bytes", dashboardJson, StringComparison.Ordinal);
        Assert.Contains("dotnet_gc_collections_total", dashboardJson, StringComparison.Ordinal);
        Assert.DoesNotContain("unipm_embedding", dashboardJson, StringComparison.Ordinal);
        Assert.DoesNotContain("unipm_search_projection", dashboardJson, StringComparison.Ordinal);
        Assert.DoesNotContain(titles, title => title!.Contains("overdue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(titles, title => title!.Contains("department performance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compose_profile_keeps_monitoring_optional_and_images_pinned()
    {
        var compose = ReadRepositoryFile("docker-compose.yml");

        Assert.Contains("prometheus:", compose, StringComparison.Ordinal);
        Assert.Contains("grafana:", compose, StringComparison.Ordinal);
        Assert.Contains("- observability", compose, StringComparison.Ordinal);
        Assert.Contains("prom/prometheus:v3.5.0", compose, StringComparison.Ordinal);
        Assert.Contains("grafana/grafana:12.0.2", compose, StringComparison.Ordinal);
        Assert.Contains("Observability__MetricsEnabled", compose, StringComparison.Ordinal);
        Assert.DoesNotContain(":latest", compose, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }
}
