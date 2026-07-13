using System.Text.Json;
using Json.Schema;

namespace UniPM.Api.Tests.MaintenanceReview;

public sealed class DeepSeekSummaryExperimentManifestTests
{
    private static readonly string FixtureDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "MaintenanceReview",
        "Fixtures");

    [Fact]
    public async Task Manifest_validates_and_contains_the_bounded_multilingual_case_set()
    {
        var manifestPath = Path.Combine(FixtureDirectory, "deepseek-v4-summary-experiment-v1.json");
        var schemaPath = Path.Combine(FixtureDirectory, "deepseek-v4-summary-experiment-v1.schema.json");
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(schemaPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));

        var evaluation = schema.Evaluate(document.RootElement);
        Assert.True(evaluation.IsValid, "DeepSeek summary experiment manifest failed schema validation.");

        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(12, cases.Length);
        Assert.Equal(12, cases.Select(item => item.GetProperty("caseId").GetString()).Distinct().Count());
        Assert.All(["english", "tagalog", "taglish"], language =>
            Assert.Equal(4, cases.Count(item => item.GetProperty("language").GetString() == language)));
        Assert.Contains(cases, item => item.GetProperty("recurrenceMayBeStated").GetBoolean());
        Assert.Contains(cases, item => !item.GetProperty("recurrenceMayBeStated").GetBoolean());
        Assert.Contains(cases, item =>
            item.GetProperty("expectedEvidenceCondition").GetString() == "same_category_fallback");
        Assert.Single(cases, item =>
            item.TryGetProperty("testSetup", out var setup)
            && setup.GetString() == "prompt-injection-source");
    }

    [Fact]
    public async Task Every_case_references_an_asset_in_the_immutable_operational_fixture()
    {
        var manifestPath = Path.Combine(FixtureDirectory, "deepseek-v4-summary-experiment-v1.json");
        var operationalPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Seeding",
            "Resources",
            "synthetic-maintenance-v1.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        using var operational = JsonDocument.Parse(await File.ReadAllTextAsync(operationalPath));
        var assetIds = operational.RootElement.GetProperty("assets")
            .EnumerateArray()
            .Select(asset => asset.GetProperty("id").GetGuid())
            .ToHashSet();

        Assert.All(manifest.RootElement.GetProperty("cases").EnumerateArray(), item =>
            Assert.Contains(item.GetProperty("assetId").GetGuid(), assetIds));
    }

    [Fact]
    public async Task Manifest_contains_only_evaluation_metadata_and_no_provider_payloads()
    {
        var manifestPath = Path.Combine(FixtureDirectory, "deepseek-v4-summary-experiment-v1.json");
        var text = await File.ReadAllTextAsync(manifestPath);

        Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"authorization\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("systemMessage", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userMessage", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenMap", text, StringComparison.OrdinalIgnoreCase);
    }
}
