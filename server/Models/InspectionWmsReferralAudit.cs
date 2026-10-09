namespace UniPM.Api.Models;

public sealed class InspectionWmsReferralAudit
{
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public InspectionRecord? Inspection { get; set; }
    public string? PreviousExternalPmNumber { get; set; }
    public string NewExternalPmNumber { get; set; } = string.Empty;
    public int Revision { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
