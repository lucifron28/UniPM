using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

internal static class ScheduleAssignmentEventRecorder
{
    internal static async Task<HashSet<Guid>> FindHistoryScheduleIdsAsync(
        ApplicationDbContext context,
        IReadOnlyCollection<Guid> scheduleIds,
        string assignmentRole,
        CancellationToken cancellationToken)
    {
        if (scheduleIds.Count == 0)
        {
            return [];
        }

        return (await context.ScheduleAssignmentReassignmentAudits
                .AsNoTracking()
                .Where(audit => scheduleIds.Contains(audit.ScheduleId)
                    && audit.AssignmentRole == assignmentRole)
                .Select(audit => audit.ScheduleId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    internal static void RecordChange(
        ApplicationDbContext context,
        PreventiveMaintenanceSchedule schedule,
        string assignmentRole,
        Guid? previousAssigneeUserId,
        Guid? newAssigneeUserId,
        Guid changedByUserId,
        DateTimeOffset changedAt,
        string reason,
        bool hasPriorAssignmentHistory,
        bool isReassignmentOperation = false)
    {
        if (previousAssigneeUserId == newAssigneeUserId)
        {
            return;
        }

        var firstAssignedAt = assignmentRole switch
        {
            "Supervisor" => schedule.FirstSupervisorAssignedAt,
            "Inspector" => schedule.FirstInspectorAssignedAt,
            _ => throw new ArgumentOutOfRangeException(nameof(assignmentRole))
        };
        var isInitialAssignment = previousAssigneeUserId is null
            && newAssigneeUserId is not null
            && firstAssignedAt is null
            && !hasPriorAssignmentHistory
            && !isReassignmentOperation;

        if (isInitialAssignment)
        {
            if (assignmentRole == "Supervisor")
            {
                schedule.FirstSupervisorAssignedAt ??= changedAt;
            }
            else
            {
                schedule.FirstInspectorAssignedAt ??= changedAt;
            }
        }

        var eventType = newAssigneeUserId is null
            ? ScheduleAssignmentEventTypeCatalog.Unassignment
            : isInitialAssignment
                ? ScheduleAssignmentEventTypeCatalog.InitialAssignment
                : ScheduleAssignmentEventTypeCatalog.Reassignment;

        context.ScheduleAssignmentReassignmentAudits.Add(new ScheduleAssignmentReassignmentAudit
        {
            Id = Guid.NewGuid(),
            ScheduleId = schedule.Id,
            EventType = eventType,
            AssignmentRole = assignmentRole,
            PreviousAssigneeUserId = previousAssigneeUserId,
            NewAssigneeUserId = newAssigneeUserId,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt,
            Reason = isInitialAssignment ? "Initial assignment" : reason
        });
    }
}
