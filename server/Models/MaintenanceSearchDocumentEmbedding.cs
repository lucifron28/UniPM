namespace UniPM.Api.Models;

public sealed class MaintenanceSearchDocumentEmbedding
{
    public Guid InspectionId { get; set; }
    public MaintenanceSearchDocument? SearchDocument { get; set; }

    public string ProviderKey { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public string EmbeddingProfile { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string VectorJson { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}
