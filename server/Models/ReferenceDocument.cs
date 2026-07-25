namespace UniPM.Api.Models;

/// <summary>
/// Stores approved-reference metadata separately from operational maintenance history.
/// Reference content remains source material; no retrieval or generation behavior is implied.
/// </summary>
public sealed class ReferenceDocument
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PublisherAuthority { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public Guid? SupersededByDocumentId { get; set; }
    public ReferenceDocument? SupersededByDocument { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string ContentChecksum { get; set; } = string.Empty;
    public bool IsSynthetic { get; set; }
    public string? SyntheticFixtureKey { get; set; }
    public ICollection<ReferenceDocumentApplicability> Applicabilities { get; } = [];
    public ICollection<ReferenceDocumentSection> Sections { get; } = [];
}
