namespace UniPM.Api.Features.Retrieval;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public const string ProviderKeyValue = "openai-compatible";

    public bool Enabled { get; set; }
    public string? BaseAddress { get; set; }
    public string Path { get; set; } = "/v1/embeddings";
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxBatchSize { get; set; } = 16;
    public int MaxInputCharacters { get; set; } = 4000;
    public bool AllowRemoteProvider { get; set; }
    public int? Dimensions { get; set; }
}
