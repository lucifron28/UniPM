namespace UniPM.Api.Models;

public sealed class InspectionWmsReferral
{
    public Guid InspectionId { get; set; }
    public InspectionRecord? Inspection { get; set; }
    public string ExternalPmNumber { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Guid LastUpdatedByUserId { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
