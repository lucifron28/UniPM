using System;

namespace UniPM.Api.Models;

public class PreventiveMaintenanceSchedule
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public DateTimeOffset ScheduleDate { get; set; }
    public string PeriodType { get; set; } = "Quarter"; // Quarter, Semester, Annual, Custom
    public string Status { get; set; } = "Due"; // Due, Ongoing, Completed, Overdue, Cancelled

    // Flexible fields
    public string? Quarter { get; set; }
    public string? Semester { get; set; }
    public int? Year { get; set; }
    public string? AcademicYear { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
