using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniPM.Api.Tests;

public sealed class ReferenceDataEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ReferenceDataEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Asset_categories_returns_the_selected_study_scope()
    {
        var categories = await _client.GetFromJsonAsync<List<AssetCategoryResponse>>(
            "/api/v1/reference-data/asset-categories");

        Assert.NotNull(categories);
        Assert.Collection(
            categories,
            category => Assert.Equal(
                new("fire-extinguisher", "Fire Extinguisher"),
                category),
            category => Assert.Equal(
                new("fire-alarm", "Fire Alarm"),
                category),
            category => Assert.Equal(
                new("emergency-light", "Emergency Light"),
                category),
            category => Assert.Equal(
                new("water-drinking-station", "Water Drinking Station"),
                category));
    }

    private sealed record AssetCategoryResponse(string Code, string DisplayName);
}
