using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniPM.Api.Tests;

public sealed class InspectionOpenApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InspectionOpenApiContractTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_exposes_typed_inspection_operations()
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        AssertOperation(paths, "/api/v1/inspections", "post", "RecordInspection", "201", "InspectionResponse");
        AssertArrayOperation(paths, "/api/v1/inspections", "get", "ListInspections", "InspectionResponse");
        AssertOperation(paths, "/api/v1/inspections/{id}", "get", "GetInspection", "200", "InspectionResponse");
        AssertArrayOperation(paths, "/api/v1/inspections/history/{assetId}", "get", "GetInspectionHistory", "InspectionHistoryResponse");
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
