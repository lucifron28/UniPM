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
    
    // These fields will be indexed for Full-Text Search and Vector Search
    public string? Remarks { get; set; }
    public string? ActionsRecommendations { get; set; }

    // RAG vector embeddings
    public string? RemarksEmbedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
