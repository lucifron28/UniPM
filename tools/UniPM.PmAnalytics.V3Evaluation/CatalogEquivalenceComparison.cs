using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniPM.PmAnalytics.V3Evaluation;

// Auxiliary scoring only. Production lookup behavior and strict scoring are unchanged.
internal static class CatalogEquivalenceComparison
{
    internal const string Version = "catalog-equivalence-v1";
    internal const string Building = "North Block";
    internal const string Department = "Synthetic Department";
    internal static readonly string Definition = JsonSerializer.Serialize(new
    {
        version = Version,
        rule = "Trim and invariant-uppercase only known population-filter values; preserve all other JSON and array order",
        fixture = new { building = new[] { Building }, department = new[] { Department }, location = Array.Empty<string>() },
        scope = "One fictional fire-extinguisher asset; each listed value resolves to that same unique catalog scope"
    });
    internal static readonly string Fingerprint = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Definition))).ToLowerInvariant();

    internal static JsonElement Normalize(JsonElement source)
    {
        var node = JsonNode.Parse(source.GetRawText());
        if (node is JsonObject root && root["baseFilters"] is JsonArray filters)
        {
            foreach (var filter in filters.OfType<JsonObject>())
            {
                if (filter["field"] is not JsonValue field || !field.TryGetValue<string>(out var name)
                    || filter["values"] is not JsonArray values) continue;
                var known = name switch
                {
                    "building" => Building.ToUpperInvariant(),
                    "department" => Department.ToUpperInvariant(),
                    _ => null
                };
                if (known is null) continue;
                for (var index = 0; index < values.Count; index++)
                {
                    if (values[index] is JsonValue value && value.TryGetValue<string>(out var text) && text is not null
                        && text.Trim().ToUpperInvariant() == known)
                        values[index] = known;
                }
            }
        }
        return JsonSerializer.SerializeToElement(node);
    }
}
