namespace UniPM.Api.Models;

public sealed class ScheduleAssignmentReassignmentAudit
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public Guid? PreviousAssigneeUserId { get; set; }
    public Guid NewAssigneeUserId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}
