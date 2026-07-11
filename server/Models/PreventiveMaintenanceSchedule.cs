using System;

namespace UniPM.Api.Models;

public class PreventiveMaintenanceSchedule
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public DateTimeOffset ScheduleDate { get; set; }

    /// <summary>
    /// Stores the canonical maintenance-period code normalized by SchedulePeriodTypeCatalog.
    /// </summary>
    public string PeriodType { get; set; } = "Quarter";

    /// <summary>
    /// Stores the canonical controlled status from ScheduleStatusCatalog.
    /// Persisted statuses may be broader than values currently written by API commands;
    /// allowed statuses do not imply implemented transition workflows.
    /// </summary>
    public string Status { get; set; } = "Due";

    /// <summary>
    /// Stores controlled quarter metadata normalized by ScheduleQuarterCatalog.
    /// it does not finalize scheduling policy.
    /// </summary>
    public string? Quarter { get; set; }

    /// <summary>
    /// Stores controlled semester metadata from ScheduleSemesterCatalog.
    /// Persisted metadata may be broader than values currently written by API commands;
    /// it does not finalize scheduling policy.
    /// </summary>
    public string? Semester { get; set; }
    public int? Year { get; set; }
    public string? AcademicYear { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
