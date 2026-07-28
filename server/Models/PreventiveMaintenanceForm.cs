namespace UniPM.Api.Models;

/// <summary>
/// Stores one digital representation of a preventive-maintenance form header.
/// Its inspection rows are stored as separate InspectionRecord entities.
/// </summary>
public sealed class PreventiveMaintenanceForm
{
    public Guid Id { get; set; }
    public string? FileNumber { get; set; }
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string? Quarter { get; set; }
    public string? Semester { get; set; }
    public int? Year { get; set; }
    public string? AcademicYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public ICollection<InspectionRecord> Inspections { get; } = [];
    public PreventiveMaintenanceAcknowledgement? Acknowledgement { get; set; }
}
