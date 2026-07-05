using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniPM.Api.Tests;

public sealed class ValidationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ValidationEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_asset_rejects_missing_required_fields_before_database_access()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = "",
            assetCategory = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("AssetCode", problem.Errors.Keys);
        Assert.Contains("AssetCategory", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_asset_rejects_categories_outside_current_scope()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/assets/", new
        {
            assetCode = "HVAC-001",
            assetCategory = "hvac"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("AssetCategory", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_schedule_rejects_missing_asset_and_period_fields_before_database_access()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/schedules/", new
        {
            assetId = Guid.Empty,
            scheduleDate = DateTimeOffset.UtcNow,
            periodType = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("AssetId", problem.Errors.Keys);
        Assert.Contains("PeriodType", problem.Errors.Keys);
    }

    [Fact]
    public async Task Record_inspection_rejects_missing_required_fields_before_database_access()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inspections/", new
        {
            scheduleId = Guid.Empty,
            inspectorUserId = Guid.Empty,
            dateInspected = default(DateTimeOffset),
            isOperational = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("ScheduleId", problem.Errors.Keys);
        Assert.Contains("InspectorUserId", problem.Errors.Keys);
        Assert.Contains("DateInspected", problem.Errors.Keys);
    }
}
