using System.Text.Json.Serialization;

namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticReferenceDocumentDataset
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }
    public string DatasetVersion { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<SyntheticReferenceDocument> Documents { get; set; } = [];
}

public sealed class SyntheticReferenceDocument
{
    public string SeedKey { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PublisherAuthority { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public Guid? SupersededByDocumentId { get; set; }
    public List<SyntheticReferenceApplicability> Applicabilities { get; set; } = [];
    public List<SyntheticReferenceSection> Sections { get; set; } = [];
}

public sealed class SyntheticReferenceApplicability
{
    public string? AssetCategory { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelSeries { get; set; }
    public string? EquipmentFamily { get; set; }
    public string? ScopeLabel { get; set; }
}

public sealed class SyntheticReferenceSection
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string SourceLocator { get; set; } = string.Empty;
    public int? PageStart { get; set; }
    public int? PageEnd { get; set; }
    public string SectionText { get; set; } = string.Empty;
}
