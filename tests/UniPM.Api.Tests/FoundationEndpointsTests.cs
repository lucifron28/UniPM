using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace UniPM.Api.Tests;

public sealed class FoundationEndpointsTests : IClassFixture<FoundationEndpointsTests.TestApplicationFactory>
{
    private readonly HttpClient _client;

    public FoundationEndpointsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_returns_api_information()
    {
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiInformation>();

        Assert.NotNull(payload);
        Assert.Equal("UniPM API", payload.Name);
        Assert.Equal("running", payload.Status);
    }

    [Fact]
    public async Task Liveness_returns_ok_without_database()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_service_unavailable_without_database_configuration()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Template_weather_route_is_not_exposed()
    {
        var response = await _client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ApiInformation(string Name, string Status);

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        }
    }
}
