namespace UniPM.Api.Models;

/// <summary>
/// Records a current-year asset cycle that could not join its canonical PM batch
/// because work had already started in that batch.
/// </summary>
public sealed class ScheduleEnrollmentDeferral
{
    public Guid AssetId { get; set; }
    public string PmCycle { get; set; } = string.Empty;
    public string DepartmentAtDeferral { get; set; } = string.Empty;
    public string AssetCategoryAtDeferral { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public DateTimeOffset DeferredAt { get; set; }
    public string NextEligiblePmCycle { get; set; } = string.Empty;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
}
