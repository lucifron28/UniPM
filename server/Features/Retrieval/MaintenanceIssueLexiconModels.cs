using System.Text.Json.Serialization;

namespace UniPM.Api.Features.Retrieval;

public sealed class MaintenanceIssueLexiconDocument
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string LexiconVersion { get; set; } = string.Empty;
    public List<MaintenanceIssueDefinition> Issues { get; set; } = [];
}

public sealed class MaintenanceIssueDefinition
{
    public string Key { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = [];
}

public sealed record MaintenanceIssueMatch(
    string IssueKey,
    int Score,
    IReadOnlyList<string> MatchedAliases);
