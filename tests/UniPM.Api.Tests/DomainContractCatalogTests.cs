using System.Text.Json.Nodes;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Tests;

public sealed class DomainContractCatalogTests
{
    [Fact]
    public void Catalogs_match_the_static_fixture_schema_enums()
    {
        var schema = LoadFixtureSchema();

        AssertSetEqual(
            SchemaEnum(schema, "assetCategory"),
            AssetCategoryCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "asset", "status"),
            AssetStatusCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "schedule", "periodType"),
            SchedulePeriodTypeCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "schedule", "status"),
            ScheduleStatusCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "schedule", "quarter"),
            ScheduleQuarterCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "schedule", "semester"),
            ScheduleSemesterCatalog.PersistedCodes);
        AssertSetEqual(
            SchemaPropertyEnum(schema, "actor", "roleToken"),
            SyntheticActorRoleCatalog.SeedOnlyCodes);
    }

    [Fact]
    public void Catalogs_distinguish_persisted_codes_from_current_api_command_codes()
    {
        AssertSetEqual([AssetStatusCatalog.Active], AssetStatusCatalog.ApiWritableCodes);
        AssertSetEqual(
            [ScheduleStatusCatalog.Due, ScheduleStatusCatalog.Completed],
            ScheduleStatusCatalog.ApiWritableCodes);
        AssertSetEqual(
            SchedulePeriodTypeCatalog.PersistedCodes,
            SchedulePeriodTypeCatalog.ApiWritableCodes);
        AssertSetEqual(
            ScheduleQuarterCatalog.PersistedCodes,
            ScheduleQuarterCatalog.ApiWritableCodes);
        Assert.Empty(ScheduleSemesterCatalog.ApiWritableCodes);
    }

    [Fact]
    public void Catalogs_accept_case_and_whitespace_but_return_canonical_codes()
    {
        Assert.True(AssetCategoryCatalog.TryNormalize(" FIRE-ALARM ", out var category));
        Assert.Equal(AssetCategoryCatalog.FireAlarm, category);

        Assert.True(AssetStatusCatalog.TryNormalize(" active ", out var assetStatus));
        Assert.Equal(AssetStatusCatalog.Active, assetStatus);

        Assert.True(SchedulePeriodTypeCatalog.TryNormalize(" quarter ", out var periodType));
        Assert.Equal(SchedulePeriodTypeCatalog.Quarter, periodType);

        Assert.True(ScheduleQuarterCatalog.TryNormalizeNullable(" q2 ", out var quarter));
        Assert.Equal(ScheduleQuarterCatalog.Q2, quarter);

        Assert.False(ScheduleStatusCatalog.TryNormalize("Paused", out _));
        Assert.False(ScheduleQuarterCatalog.TryNormalizeNullable("Q5", out _));
    }

    private static JsonObject LoadFixtureSchema()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(
            root,
            "server",
            "Data",
            "Seeding",
            "Resources",
            "synthetic-maintenance-v1.schema.json");

        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    private static IEnumerable<string?> SchemaEnum(JsonObject schema, string definition)
    {
        return schema["$defs"]![definition]!["enum"]!
            .AsArray()
            .Select(value => value?.GetValue<string>());
    }

    private static IEnumerable<string?> SchemaPropertyEnum(
        JsonObject schema,
        string definition,
        string property)
    {
        return schema["$defs"]![definition]!["properties"]![property]!["enum"]!
            .AsArray()
            .Select(value => value?.GetValue<string>());
    }

    private static void AssertSetEqual(
        IEnumerable<string?> expected,
        IEnumerable<string> actual)
    {
        var expectedValues = expected
            .Where(value => value is not null)
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(expectedValues.SetEquals(actual),
            $"Expected [{string.Join(", ", expectedValues)}], got [{string.Join(", ", actual)}].");
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

        throw new DirectoryNotFoundException("Unable to locate the UniPM repository root.");
    }
}
