using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace UniPM.Api.Tests.Hosting;

public sealed class PackagedFrontendHostingTests
{
    [Fact]
    public async Task Packaged_frontend_serves_routes_without_hiding_api_or_health_responses()
    {
        using var webRoot = new TemporaryWebRoot();
        using var factory = new HostingFactory(
            webRoot.Path,
            "https://staging.unipm.example");
        using var client = factory.CreateClient();

        var root = await client.GetAsync("/");
        var nestedRoute = await client.GetAsync("/reports/overview");
        var apiInfo = await client.GetAsync("/api/info");
        var unknownApi = await client.GetAsync("/api/v1/not-a-route");
        var missingAsset = await client.GetAsync("/assets/missing.js");
        var hashedAsset = await client.GetAsync("/assets/main-A2b3-C_4.js");
        var unhashedAsset = await client.GetAsync("/assets/logo.svg");
        var liveness = await client.GetAsync("/health/live");
        var readiness = await client.GetAsync("/health/ready");
        var postToRoute = await client.PostAsync("/reports/overview", new StringContent(""));

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("packaged frontend", await root.Content.ReadAsStringAsync());
        AssertHtmlCachePolicy(root);
        Assert.Equal(HttpStatusCode.OK, nestedRoute.StatusCode);
        Assert.Contains("packaged frontend", await nestedRoute.Content.ReadAsStringAsync());
        AssertHtmlCachePolicy(nestedRoute);

        Assert.Equal(HttpStatusCode.OK, apiInfo.StatusCode);
        var apiPayload = await apiInfo.Content.ReadFromJsonAsync<ApiInformation>();
        Assert.NotNull(apiPayload);
        Assert.Equal("UniPM API", apiPayload.Name);
        Assert.Equal("running", apiPayload.Status);

        Assert.Equal(HttpStatusCode.NotFound, unknownApi.StatusCode);
        Assert.DoesNotContain("packaged frontend", await unknownApi.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, postToRoute.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", hashedAsset.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", unhashedAsset.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://staging.unipm.example")]
    [InlineData("https://localhost")]
    public void Production_startup_rejects_missing_or_nonpublic_https_web_origin(string? webOrigin)
    {
        using var factory = new HostingFactory(
            webRootPath: null,
            webOrigin: webOrigin,
            environmentName: "Production");

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("UNIPM_WEB_ORIGIN", exception.ToString());
    }

    private sealed record ApiInformation(string Name, string Status);

    private static void AssertHtmlCachePolicy(HttpResponseMessage response)
    {
        var policy = response.Headers.CacheControl;
        Assert.NotNull(policy);
        Assert.True(policy.NoStore);
        Assert.True(policy.NoCache);
        Assert.True(policy.MustRevalidate);
    }

    private sealed class HostingFactory(
        string? webRootPath,
        string? webOrigin,
        string environmentName = "Production") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
            if (webRootPath is not null) builder.UseWebRoot(webRootPath);
            builder.DisableScheduleGenerationWorker();
            builder.ConfigureLogging(logging => logging.ClearProviders());
            // Program validates these values before ConfigureAppConfiguration callbacks run.
            foreach (var setting in new Dictionary<string, string>
                {
                    ["UNIPM_JWT_ISSUER"] = "UniPM.HostingTests",
                    ["UNIPM_JWT_AUDIENCE"] = "UniPM.HostingTests.Clients",
                    ["UNIPM_JWT_SIGNING_KEY"] = "HostingTestsRequireSigningKeyAtLeast32Bytes!",
                    ["UNIPM_JWT_ACCESS_TOKEN_MINUTES"] = "60",
                    ["UNIPM_WEB_ORIGIN"] = webOrigin ?? string.Empty,
                    ["PreventiveMaintenanceScheduleGeneration:WorkerEnabled"] = "false",
                    ["InspectionPhotoEvidence:StorageProvider"] = "FileSystem"
                })
            {
                builder.UseSetting(setting.Key, setting.Value);
            }
        }
    }

    private sealed class TemporaryWebRoot : IDisposable
    {
        private readonly string _path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"unipm-hosting-test-{Guid.NewGuid():N}");

        public string Path => _path;

        public TemporaryWebRoot()
        {
            Directory.CreateDirectory(_path);
            File.WriteAllText(
                System.IO.Path.Combine(_path, "index.html"),
                "<!doctype html><title>packaged frontend</title>");

            var assets = System.IO.Path.Combine(_path, "assets");
            Directory.CreateDirectory(assets);
            File.WriteAllText(System.IO.Path.Combine(assets, "main-A2b3-C_4.js"), "window.app = true;");
            File.WriteAllText(System.IO.Path.Combine(assets, "logo.svg"), "<svg></svg>");
        }

        public void Dispose()
        {
            var fullPath = System.IO.Path.GetFullPath(_path);
            var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            if (fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
                && System.IO.Path.GetFileName(fullPath).StartsWith(
                    "unipm-hosting-test-",
                    StringComparison.Ordinal))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
    }
}
