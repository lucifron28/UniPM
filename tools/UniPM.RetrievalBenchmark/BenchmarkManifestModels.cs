using System.Text.Json.Serialization;

namespace UniPM.RetrievalBenchmark;

public sealed class RetrievalEvaluationManifest
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    public string EvaluationVersion { get; set; } = string.Empty;
    public string DatasetVersion { get; set; } = string.Empty;
    public List<RetrievalEvaluationAssetAnnotation> AssetAnnotations { get; set; } = [];
    public List<RetrievalEvaluationRecordAnnotation> RecordAnnotations { get; set; } = [];
    public List<RetrievalEvaluationQuery> Queries { get; set; } = [];
}

public sealed class RetrievalEvaluationAssetAnnotation
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}

public sealed class RetrievalEvaluationRecordAnnotation
{
    public Guid InspectionId { get; set; }
    public string InspectionSeedKey { get; set; } = string.Empty;
    public List<string> ExpectedIssueKeys { get; set; } = [];
    public List<string> ScenarioTags { get; set; } = [];
}

public sealed class RetrievalEvaluationQuery
{
    public string QueryId { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public Guid? ContextAssetId { get; set; }
    public required RetrievalFilters RetrievalFilters { get; set; }
    public List<Guid> ExpectedRelevantInspectionIds { get; set; } = [];
    public List<string> ScenarioTags { get; set; } = [];
    public string Notes { get; set; } = string.Empty;
}

public sealed class RetrievalFilters
{
    public Guid? AssetId { get; set; }
    public string? AssetCategory { get; set; }
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public bool? IsOperational { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
}

public static class RetrievalBenchmarkVocabulary
{
    public const string EvaluationVersion = "1.1.0";
    public const string DatasetVersion = "1.1.0";
    public const string SchemaFileName = "retrieval-evaluation-v1.schema.json";
    public const string ManifestFileName = "retrieval-evaluation-v1.json";

    public static IReadOnlySet<string> Languages { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "english",
        "tagalog",
        "taglish"
    };

    public static IReadOnlySet<string> QueryScenarioTags { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "same-asset-history",
        "similar-asset-fallback",
        "cold-start",
        "lexicon-covered",
        "semantic-paraphrase",
        "distractor-resistance",
        "same-building-context",
        "cross-language",
        "resolved-history",
        "unresolved-history"
    };

    public static IReadOnlySet<string> RequiredCoverageTags { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "same-asset-history",
        "similar-asset-fallback",
        "cold-start",
        "semantic-paraphrase",
        "distractor-resistance"
    };
}

public sealed class RetrievalEvaluationManifestException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
