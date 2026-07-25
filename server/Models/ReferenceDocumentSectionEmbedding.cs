namespace UniPM.Api.Models;

/// <summary>
/// Deferred retrieval data keyed to a section hash so stale vectors are detectable.
/// This record never stores provider credentials, query vectors, or provider payloads.
/// </summary>
public sealed class ReferenceDocumentSectionEmbedding
{
    public Guid ReferenceDocumentSectionId { get; set; }
    public ReferenceDocumentSection? ReferenceDocumentSection { get; set; }
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public string EmbeddingProfile { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string VectorJson { get; set; } = string.Empty;
    public string SectionHash { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}
