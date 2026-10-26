namespace UniPM.Api.Models;

public sealed class SavedAnalyticsReport
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlanJson { get; set; } = string.Empty;
    public string PeriodMode { get; set; } = "Fixed";
    public Guid Revision { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
