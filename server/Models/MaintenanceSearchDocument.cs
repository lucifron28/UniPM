namespace UniPM.Api.Models;

public sealed class MaintenanceSearchDocument
{
    public Guid InspectionId { get; set; }
    public InspectionRecord? Inspection { get; set; }

    public Guid AssetId { get; set; }
    public Guid ScheduleId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }

    public DateTimeOffset DateInspected { get; set; }
    public bool IsOperational { get; set; }
    public DateTimeOffset SourceCreatedAt { get; set; }
    public DateTimeOffset SourceUpdatedAt { get; set; }
    public DateTimeOffset AssetUpdatedAt { get; set; }

    public string ProjectionVersion { get; set; } = string.Empty;
    public string LexiconVersion { get; set; } = string.Empty;
    public string IssueKeysJson { get; set; } = "[]";
    public string SearchText { get; set; } = string.Empty;
}
