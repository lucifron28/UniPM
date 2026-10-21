using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace UniPM.Api.Tests.AdminManagement;

public sealed class AdminOpenApiContractTests
{
    [Fact]
    public async Task Admin_endpoints_are_present_with_typed_contracts()
    {
        await using var application = new OpenApiApplicationFactory();
        using var client = application.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");
        var contracts = new (string Path, string Method, string OperationId, string Status, string ResponseSchema)[]
        {
            ("/api/v1/admin/overview", "get", "GetAdminOverview", "200", "AdminOverviewResponse"),
            ("/api/v1/admin/accounts", "get", "ListAdminManagedAccounts", "200", "AdminAccountPageResponse"),
            ("/api/v1/admin/accounts", "post", "CreateAdminManagedAccount", "201", "AdminAccountResponse"),
            ("/api/v1/admin/assets", "get", "ListAdminAssets", "200", "AdminAssetPageResponse"),
            ("/api/v1/admin/assets", "post", "CreateAdminAsset", "201", "AdminAssetRow"),
            ("/api/v1/admin/maintenance/schedules", "get", "ListAdminSchedules", "200", "AdminSchedulePageResponse"),
            ("/api/v1/admin/maintenance/inspections", "get", "ListAdminInspections", "200", "AdminInspectionPageResponse"),
            ("/api/v1/admin/maintenance/forms", "get", "ListAdminForms", "200", "AdminFormPageResponse"),
            ("/api/v1/admin/demo-data/validation/load", "post", "LoadAdminValidationDataset", "200", "AdminValidationDatasetResponse"),
            ("/api/v1/admin/demo-data/validation/reset", "post", "ResetAndSeedAdminValidationDataset", "200", "AdminValidationDatasetResponse"),
        };

        foreach (var (path, method, operationId, status, responseSchema) in contracts)
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
            var schema = operation.GetProperty("responses").GetProperty(status)
                .GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.Equal($"#/components/schemas/{responseSchema}", schema.GetProperty("$ref").GetString());
        }

        var scheduleParameters = paths.GetProperty("/api/v1/admin/maintenance/schedules")
            .GetProperty("get").GetProperty("parameters");
        Assert.Contains(scheduleParameters.EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "status");

        var accountRequest = paths.GetProperty("/api/v1/admin/accounts").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").GetProperty("application/json")
            .GetProperty("schema").GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/AdminCreateAccountRequest", accountRequest);
    }

    private sealed class OpenApiApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.DisableScheduleGenerationWorker();
        }
    }
}
