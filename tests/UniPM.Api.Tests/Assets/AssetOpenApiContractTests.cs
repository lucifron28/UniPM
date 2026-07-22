using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniPM.Api.Tests;

public sealed class AssetOpenApiContractTests : IClassFixture<AssetOpenApiContractTests.TestApplicationFactory>
{
    private readonly HttpClient _client;

    public AssetOpenApiContractTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Asset_operations_expose_typed_public_contracts()
    {
        using var document = JsonDocument.Parse(
            await _client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        AssertOperation(paths, "/api/v1/assets", "post", "CreateAsset", "201", "AssetResponse");
        AssertOperation(paths, "/api/v1/assets", "get", "ListAssets", "200", null);
        AssertOperation(paths, "/api/v1/assets/{id}", "get", "GetAsset", "200", "AssetResponse");
        AssertOperation(paths, "/api/v1/assets/by-qr/{qrCodeValue}", "get", "GetAssetByQr", "200", "AssetResponse");

        var create = paths.GetProperty("/api/v1/assets").GetProperty("post").GetProperty("responses");
        Assert.True(create.TryGetProperty("400", out _));
        Assert.True(create.TryGetProperty("403", out _));
        Assert.True(create.TryGetProperty("409", out _));

        var list = paths.GetProperty("/api/v1/assets").GetProperty("get").GetProperty("responses");
        Assert.True(list.TryGetProperty("400", out _));
        var listSchema = list.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", listSchema.GetProperty("type").GetString());
        Assert.EndsWith("/AssetResponse", listSchema.GetProperty("items").GetProperty("$ref").GetString());

        var detail = paths.GetProperty("/api/v1/assets/{id}").GetProperty("get").GetProperty("responses");
        var qr = paths.GetProperty("/api/v1/assets/by-qr/{qrCodeValue}").GetProperty("get").GetProperty("responses");
        Assert.True(detail.TryGetProperty("404", out _));
        Assert.True(qr.TryGetProperty("400", out _));
        Assert.True(qr.TryGetProperty("404", out _));
    }

    private static void AssertOperation(
        JsonElement paths,
        string path,
        string method,
        string operationId,
        string status,
        string? schemaName)
    {
        var operation = paths.GetProperty(path).GetProperty(method);
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        var schema = operation.GetProperty("responses").GetProperty(status)
            .GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (schemaName is not null)
        {
            Assert.EndsWith($"/{schemaName}", schema.GetProperty("$ref").GetString());
        }
    }

    public sealed class TestApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
