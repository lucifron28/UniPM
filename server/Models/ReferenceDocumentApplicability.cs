namespace UniPM.Api.Models;

/// <summary>
/// Stores bounded applicability metadata. Manufacturer/model/series values are source metadata,
/// not a finalized institutional equipment taxonomy.
/// </summary>
public sealed class ReferenceDocumentApplicability
{
    public Guid Id { get; set; }
    public Guid ReferenceDocumentId { get; set; }
    public ReferenceDocument? ReferenceDocument { get; set; }
    public string? AssetCategory { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelSeries { get; set; }
    public string? EquipmentFamily { get; set; }
    public string? ScopeLabel { get; set; }
}
