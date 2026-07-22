using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniPM.Api.Tests;

public sealed class ScheduleOpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ScheduleOpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_exposes_typed_schedule_and_reference_operations()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        AssertOperation(paths, "/api/v1/schedules", "post", "CreateSchedule", "201", "ScheduleResponse");
        AssertArrayOperation(paths, "/api/v1/schedules", "get", "ListSchedules", "ScheduleResponse");
        AssertOperation(paths, "/api/v1/schedules/{id}", "get", "GetSchedule", "200", "ScheduleResponse");
        AssertArrayOperation(paths, "/api/v1/reference-data/schedule-statuses", "get", "ListScheduleStatuses", "ScheduleReferenceResponse");
        AssertArrayOperation(paths, "/api/v1/reference-data/schedule-period-types", "get", "ListSchedulePeriodTypes", "ScheduleReferenceResponse");
        AssertArrayOperation(paths, "/api/v1/reference-data/schedule-quarters", "get", "ListScheduleQuarters", "ScheduleReferenceResponse");
    }

    private static void AssertOperation(
        JsonElement paths,
        string path,
        string method,
        string operationId,
        string status,
        string schemaName)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        var schema = operation.GetProperty("responses")
            .GetProperty(status)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.Equal($"#/components/schemas/{schemaName}", schema.GetProperty("$ref").GetString());
    }

    private static void AssertArrayOperation(
        JsonElement paths,
        string path,
        string method,
        string operationId,
        string itemSchemaName)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        var schema = operation.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.Equal("array", schema.GetProperty("type").GetString());
        Assert.Equal(
            $"#/components/schemas/{itemSchemaName}",
            schema.GetProperty("items").GetProperty("$ref").GetString());
    }
}
