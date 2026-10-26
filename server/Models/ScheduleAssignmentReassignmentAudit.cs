namespace UniPM.Api.Models;

public static class ScheduleAssignmentEventTypeCatalog
{
    public const string InitialAssignment = "InitialAssignment";
    public const string Reassignment = "Reassignment";
    public const string Unassignment = "Unassignment";
}

public sealed class ScheduleAssignmentReassignmentAudit
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string EventType { get; set; } = ScheduleAssignmentEventTypeCatalog.Reassignment;
    public string AssignmentRole { get; set; } = string.Empty;
    public Guid? PreviousAssigneeUserId { get; set; }
    public Guid? NewAssigneeUserId { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}
