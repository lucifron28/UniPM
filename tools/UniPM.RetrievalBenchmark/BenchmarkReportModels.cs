namespace UniPM.RetrievalBenchmark;

public sealed class BenchmarkReport
{
    public string BenchmarkFormatVersion { get; set; } = "1.0.0";
    public string EvaluationManifestVersion { get; set; } = string.Empty;
    public string OperationalDatasetVersion { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public List<string> SelectedChannels { get; set; } = [];
    public int QueryCount { get; set; }
    public Dictionary<string, BenchmarkChannelReport> Channels { get; set; } = new(StringComparer.Ordinal);
    public List<string> Warnings { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class BenchmarkChannelReport
{
    public BenchmarkChannelMetadata Metadata { get; set; } = new();
    public AggregateRetrievalMetrics Overall { get; set; } = null!;
    public Dictionary<string, AggregateRetrievalMetrics> ByLanguage { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AggregateRetrievalMetrics> ByAssetCategory { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AggregateRetrievalMetrics> ByScenarioTag { get; set; } = new(StringComparer.Ordinal);
    public List<BenchmarkQueryReport> PerQuery { get; set; } = [];
}

public sealed record BenchmarkChannelMetadata
{
    public string RetrievalChannel { get; set; } = string.Empty;
    public int ResultLimit { get; set; }
    public int QueryCount { get; set; }
    public string? ProviderKey { get; set; }
    public string? ModelKey { get; set; }
    public int? Dimensions { get; set; }
    public string? EmbeddingProfile { get; set; }
    public bool? FullTextSearchReady { get; set; }
}

public sealed class BenchmarkQueryReport
{
    public string QueryId { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public RetrievalMetrics Metrics { get; set; } = null!;
    public List<Guid> RetrievedInspectionIds { get; set; } = [];
    public Dictionary<Guid, int> ExpectedInspectionRanks { get; set; } = new();
    public Dictionary<Guid, double> RawScores { get; set; } = new();
    public double DurationMilliseconds { get; set; }
}

public sealed record BenchmarkChannelResult(
    BenchmarkChannelMetadata Metadata,
    IReadOnlyList<BenchmarkQueryReport> QueryReports);

public sealed record BenchmarkRetrievedResult(
    Guid InspectionId,
    double RawScore);
