namespace UniPM.Api.Models;

public sealed class ReferenceDocumentSection
{
    public Guid Id { get; set; }
    public Guid ReferenceDocumentId { get; set; }
    public ReferenceDocument? ReferenceDocument { get; set; }
    public int Sequence { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string SourceLocator { get; set; } = string.Empty;
    public int? PageStart { get; set; }
    public int? PageEnd { get; set; }
    public string SectionText { get; set; } = string.Empty;
    public string SectionHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ReferenceDocumentSectionEmbedding? Embedding { get; set; }
}
