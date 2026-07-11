using System;

namespace UniPM.Api.Models;

public class InspectionRecord
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public PreventiveMaintenanceSchedule? Schedule { get; set; }
    
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public Guid InspectorUserId { get; set; }
    public DateTimeOffset DateInspected { get; set; }
    
    public bool IsOperational { get; set; }

    public string? Remarks { get; set; }
    public string? ActionsRecommendations { get; set; }

    /// <summary>
    /// Deferred retrieval field; embedding generation and storage are not part of the current contract.
    /// </summary>
    public string? RemarksEmbedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
