using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UniPM.Api.Features.Auth;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

internal static class OperationalInspectionReadScope
{
    public static IQueryable<PreventiveMaintenanceForm> WhereReadableTo(
        this IQueryable<PreventiveMaintenanceForm> forms,
        ClaimsPrincipal principal)
    {
        if (principal.IsInRole(AuthRoleCatalog.Gsd))
        {
            return forms;
        }

        if (!TryGetActorId(principal, out var actorId))
        {
            return forms.Where(_ => false);
        }

        var isInspector = principal.IsInRole(AuthRoleCatalog.Inspector);
        var isSupervisor = principal.IsInRole(AuthRoleCatalog.Supervisor);
        if (!isInspector && !isSupervisor)
        {
            return forms.Where(_ => false);
        }

        return forms.Where(form =>
            (isInspector
                && (form.CreatedByUserId == actorId
                    || form.Inspections.Any(inspection =>
                        inspection.InspectorUserId == actorId
                        || (inspection.Schedule != null
                            && inspection.Schedule.AssignedToUserId == actorId))))
            || (isSupervisor
                && form.Inspections.Any(inspection =>
                    inspection.Schedule != null
                    && inspection.Schedule.AssignedSupervisorUserId == actorId)));
    }

    public static IQueryable<InspectionRecord> WhereReadableTo(
        this IQueryable<InspectionRecord> inspections,
        ClaimsPrincipal principal,
        IQueryable<PreventiveMaintenanceForm> readableForms)
    {
        if (principal.IsInRole(AuthRoleCatalog.Gsd))
        {
            return inspections;
        }

        if (!TryGetActorId(principal, out var actorId))
        {
            return inspections.Where(_ => false);
        }

        var isInspector = principal.IsInRole(AuthRoleCatalog.Inspector);
        var isSupervisor = principal.IsInRole(AuthRoleCatalog.Supervisor);
        if (!isInspector && !isSupervisor)
        {
            return inspections.Where(_ => false);
        }

        var readableFormIds = readableForms.Select(form => form.Id);
        return inspections.Where(inspection =>
            (inspection.PreventiveMaintenanceFormId.HasValue
                && readableFormIds.Contains(inspection.PreventiveMaintenanceFormId.Value))
            || (inspection.PreventiveMaintenanceFormId == null
                && ((isInspector
                    && (inspection.InspectorUserId == actorId
                        || (inspection.Schedule != null
                            && inspection.Schedule.AssignedToUserId == actorId)))
                    || (isSupervisor
                        && inspection.Schedule != null
                        && inspection.Schedule.AssignedSupervisorUserId == actorId))));
    }

    public static IQueryable<PreventiveMaintenanceSchedule> WhereReadableSchedules(
        this IQueryable<PreventiveMaintenanceSchedule> schedules,
        ClaimsPrincipal principal)
    {
        if (principal.IsInRole(AuthRoleCatalog.Gsd))
        {
            return schedules;
        }

        if (!TryGetActorId(principal, out var actorId))
        {
            return schedules.Where(_ => false);
        }

        var isInspector = principal.IsInRole(AuthRoleCatalog.Inspector);
        var isSupervisor = principal.IsInRole(AuthRoleCatalog.Supervisor);
        if (!isInspector && !isSupervisor)
        {
            return schedules.Where(_ => false);
        }

        return schedules.Where(schedule =>
            (isInspector && schedule.AssignedToUserId == actorId)
            || (isSupervisor && schedule.AssignedSupervisorUserId == actorId));
    }

    private static bool TryGetActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}
