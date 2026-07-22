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

    [Fact]
    public async Task Schedule_reference_data_returns_controlled_codes_and_labels()
    {
        var statuses = await _client.GetFromJsonAsync<List<ScheduleReferenceResponse>>(
            "/api/v1/reference-data/schedule-statuses");
        var periodTypes = await _client.GetFromJsonAsync<List<ScheduleReferenceResponse>>(
            "/api/v1/reference-data/schedule-period-types");
        var quarters = await _client.GetFromJsonAsync<List<ScheduleReferenceResponse>>(
            "/api/v1/reference-data/schedule-quarters");

        Assert.Equal(["Due", "Ongoing", "Completed", "Overdue", "Cancelled"], statuses?.Select(value => value.Code));
        Assert.Equal(["Quarter", "Semester", "Annual", "Custom"], periodTypes?.Select(value => value.Code));
        Assert.Equal(["Q1", "Q2", "Q3", "Q4"], quarters?.Select(value => value.Code));
        Assert.All(statuses!, value => Assert.False(string.IsNullOrWhiteSpace(value.DisplayName)));
        Assert.All(periodTypes!, value => Assert.False(string.IsNullOrWhiteSpace(value.DisplayName)));
        Assert.All(quarters!, value => Assert.False(string.IsNullOrWhiteSpace(value.DisplayName)));
    }

    private sealed record AssetCategoryResponse(string Code, string DisplayName);
    private sealed record ScheduleReferenceResponse(string Code, string DisplayName);
}
