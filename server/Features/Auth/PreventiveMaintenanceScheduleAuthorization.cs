using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Auth;

internal sealed class PreventiveMaintenanceScheduleAccessRequirement : IAuthorizationRequirement
{
    public static PreventiveMaintenanceScheduleAccessRequirement Instance { get; } = new();

    private PreventiveMaintenanceScheduleAccessRequirement()
    {
    }
}

internal sealed class PreventiveMaintenanceScheduleAuthorizationHandler
    : AuthorizationHandler<PreventiveMaintenanceScheduleAccessRequirement, PreventiveMaintenanceSchedule>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PreventiveMaintenanceScheduleAccessRequirement requirement,
        PreventiveMaintenanceSchedule schedule)
    {
        if (context.User.IsInRole(AuthRoleCatalog.Gsd))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (context.User.IsInRole(AuthRoleCatalog.Inspector)
            && Guid.TryParse(subject, out var authenticatedUserId)
            && (schedule.AssignedToUserId is null
                || schedule.AssignedToUserId == authenticatedUserId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
