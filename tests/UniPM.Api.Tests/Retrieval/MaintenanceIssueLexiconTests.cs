using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class MaintenanceIssueLexiconTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string LexiconPath = Path.Combine(
        RepositoryRoot,
        "server",
        "Retrieval",
        "Resources",
        MaintenanceIssueLexiconOptions.LexiconFileName);
    private static readonly string SchemaPath = Path.Combine(
        RepositoryRoot,
        "server",
        "Retrieval",
        "Resources",
        MaintenanceIssueLexiconOptions.SchemaFileName);

    [Fact]
    public async Task Lexicon_resources_validate_against_their_schema()
    {
        Assert.True(File.Exists(LexiconPath));
        Assert.True(File.Exists(SchemaPath));

        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath));
        using var instance = JsonDocument.Parse(await File.ReadAllTextAsync(LexiconPath));

        var evaluation = schema.Evaluate(instance.RootElement);

        Assert.True(evaluation.IsValid, "The maintenance issue lexicon failed schema validation.");
    }

    [Fact]
    public void Lexicon_contains_exactly_the_approved_v1_issue_keys()
    {
        var document = LoadDocument();
        var expectedKeys = MaintenanceIssueLexiconOptions.SupportedIssueKeys;
        var suppliedKeys = document.Issues.Select(issue => issue.Key).ToHashSet(StringComparer.Ordinal);
        var json = File.ReadAllText(LexiconPath);

        Assert.True(expectedKeys.SetEquals(suppliedKeys));
        Assert.DoesNotContain("expectedIssueKeys", json, StringComparison.Ordinal);
        Assert.DoesNotContain("scenarioTags", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bell_or_strobe_issue", json, StringComparison.Ordinal);
        Assert.DoesNotContain("charging_issue", json, StringComparison.Ordinal);
        Assert.DoesNotContain("dim_light", json, StringComparison.Ordinal);
        Assert.DoesNotContain("manual_call_point_issue", json, StringComparison.Ordinal);
        Assert.DoesNotContain("missing_label", json, StringComparison.Ordinal);
        Assert.DoesNotContain("missing_seal_or_pin", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MAHINA ANG PRESSURE", "fire-extinguisher", "low_pressure")]
    [InlineData("Pressure-gauge BELOW acceptable range.", "fire-extinguisher", "low_pressure")]
    [InlineData("hindi nagrerespond ang smoke detector", "fire-alarm", "smoke_detector_issue,device_not_responding")]
    [InlineData("sira ang battery at hindi umiilaw", "emergency-light", "battery_issue,not_lighting")]
    [InlineData("barado ang filter, mahina ang daloy", "water-drinking-station", "clogged_filter,weak_water_flow")]
    public void Normalize_matches_multilingual_and_multi_issue_text(
        string text,
        string category,
        string expectedKeys)
    {
        var normalizer = CreateNormalizer();

        var matches = normalizer.Normalize(text, category);

        Assert.Equal(expectedKeys.Split(','), matches.Select(match => match.IssueKey));
    }

    [Fact]
    public void Normalize_returns_empty_for_blank_text_and_cross_category_terms()
    {
        var normalizer = CreateNormalizer();

        Assert.Empty(normalizer.Normalize("   ", "fire-extinguisher"));
        Assert.Empty(normalizer.Normalize("mahina ang pressure", "water-drinking-station"));
    }

    [Fact]
    public void Normalize_requires_a_supported_asset_category()
    {
        var normalizer = CreateNormalizer();

        Assert.Throws<ArgumentException>(() => normalizer.Normalize("low pressure", ""));
        Assert.Throws<ArgumentException>(() => normalizer.Normalize("low pressure", "hvac"));
    }

    [Fact]
    public void Normalize_scores_by_longest_alias()
    {
        var normalizer = CreateNormalizer();

        var match = Assert.Single(normalizer.Normalize("battery issue; sira ang battery", "emergency-light"));

        Assert.Equal("battery_issue", match.IssueKey);
        Assert.Equal("sira ang battery".Length, match.Score);
        Assert.Equal(["sira ang battery", "battery issue"], match.MatchedAliases);
    }

    [Fact]
    public void Normalize_uses_issue_key_as_the_tiebreaker()
    {
        var normalizer = CreateNormalizer();

        var matches = normalizer.Normalize("needs refill; expired unit", "fire-extinguisher");

        Assert.Equal(["expired_unit", "low_pressure"], matches.Select(match => match.IssueKey));
        Assert.All(matches, match => Assert.Equal(12, match.Score));
    }

    [Fact]
    public void Normalize_suppresses_known_negated_aliases()
    {
        var normalizer = CreateNormalizer();

        Assert.Empty(normalizer.Normalize("no low-pressure finding", "fire-extinguisher"));
        Assert.Empty(normalizer.Normalize("walang tagas", "water-drinking-station"));
    }

    [Fact]
    public void Normalize_preserves_positive_finding_phrases()
    {
        var normalizer = CreateNormalizer();

        var matches = normalizer.Normalize(
            "low pressure finding confirmed during inspection",
            "fire-extinguisher");

        Assert.Contains(matches, match => match.IssueKey == "low_pressure");
    }

    [Fact]
    public void Normalize_keeps_positive_aliases_and_local_matches_after_negation()
    {
        var normalizer = CreateNormalizer();

        var positive = normalizer.Normalize("low pressure", "fire-extinguisher");
        var repeated = normalizer.Normalize(
            "no low-pressure finding; low pressure remains",
            "fire-extinguisher");
        var unrelated = normalizer.Normalize(
            "no pressure issue; may tagas near the fitting",
            "water-drinking-station");
        var mixed = normalizer.Normalize(
            "walang tagas, barado ang filter",
            "water-drinking-station");

        Assert.Equal(["low_pressure"], positive.Select(match => match.IssueKey));
        Assert.Equal(["low_pressure"], repeated.Select(match => match.IssueKey));
        Assert.Equal(["leaking"], unrelated.Select(match => match.IssueKey));
        Assert.Equal(["clogged_filter"], mixed.Select(match => match.IssueKey));
    }

    [Theory]
    [InlineData("root")]
    [InlineData("issue")]
    public async Task Loader_rejects_unmapped_json_properties(string propertyKind)
    {
        var path = await CreateModifiedLexiconAsync(root =>
        {
            if (propertyKind == "root")
            {
                root["unexpected"] = true;
                return;
            }

            root["issues"]!.AsArray()[0]!.AsObject()["unexpected"] = true;
        });

        try
        {
            var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
            {
                LexiconPath = path
            });

            var exception = Assert.Throws<MaintenanceIssueLexiconException>(() => loader.Load());
            Assert.Contains("unmapped", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Loader_rejects_an_issue_with_the_wrong_supported_category()
    {
        var path = await CreateModifiedLexiconAsync(root =>
        {
            var lowPressureIssue = root["issues"]!
                .AsArray()
                .Single(issue => issue!["key"]!.GetValue<string>() == "low_pressure")!
                .AsObject();
            lowPressureIssue["assetCategory"] = "water-drinking-station";
        });

        try
        {
            var loader = new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
            {
                LexiconPath = path
            });

            var exception = Assert.Throws<MaintenanceIssueLexiconException>(() => loader.Load());
            Assert.Contains("low_pressure", exception.Message, StringComparison.Ordinal);
            Assert.Contains("fire-extinguisher", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MaintenanceIssueLexiconDocument LoadDocument()
    {
        return new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = LexiconPath
        }).Load();
    }

    private static MaintenanceIssueNormalizer CreateNormalizer()
    {
        return new MaintenanceIssueNormalizer(new MaintenanceIssueLexiconLoader(new MaintenanceIssueLexiconOptions
        {
            LexiconPath = LexiconPath
        }));
    }

    private static async Task<string> CreateModifiedLexiconAsync(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(LexiconPath))!.AsObject();
        mutate(root);

        var path = Path.Combine(Path.GetTempPath(), $"unipm-lexicon-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, root.ToJsonString());
        return path;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UniPM.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root for lexicon tests.");
    }
}
