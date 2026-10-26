using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.AdminManagement;

public static class AdminAccountEndpoints
{
    private static readonly string[] ManagedRoles =
        [AuthRoleCatalog.Gsd, AuthRoleCatalog.Supervisor, AuthRoleCatalog.Inspector];

    private static readonly string[] CreateableRoles =
        [AuthRoleCatalog.Supervisor, AuthRoleCatalog.Inspector];

    public static IEndpointRouteBuilder MapAdminAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/accounts")
            .WithTags("Admin account management")
            .RequireAuthorization(AuthPolicyCatalog.CanManageAdminManagement);

        group.MapGet("/", ListAccountsAsync)
            .WithName("ListAdminManagedAccounts")
            .Produces<AdminAccountPageResponse>()
            .ProducesValidationProblem();

        group.MapPost("/", CreateAccountAsync)
            .WithName("CreateAdminManagedAccount")
            .Produces<AdminAccountResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateAccountAsync)
            .WithName("UpdateAdminManagedAccount")
            .Produces<AdminAccountResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reset-password", ResetPasswordAsync)
            .WithName("ResetAdminManagedAccountPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAccountAsync)
            .WithName("DeleteAdminManagedAccount")
            .Produces<AdminAccountDeleteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/gsd-head", GetGsdHeadAsync)
            .WithName("GetAdminGsdHead")
            .Produces<AdminGsdHeadResponse>();

        group.MapPatch("/gsd-head", UpdateGsdHeadAsync)
            .WithName("UpdateAdminGsdHead")
            .Produces<AdminAccountResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/gsd-head", ReplaceGsdHeadAsync)
            .WithName("ReplaceAdminGsdHead")
            .Produces<AdminGsdHeadResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListAccountsAsync(
        string? search,
        string? role,
        bool? active,
        int? page,
        int? pageSize,
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var pageNumber = page ?? 1;
        var size = pageSize ?? 25;
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        var errors = new Dictionary<string, string[]>();
        if (normalizedSearch?.Length > 256)
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        if (normalizedRole is not null && !ManagedRoles.Contains(normalizedRole, StringComparer.Ordinal))
            errors[nameof(role)] = ["Role must be GSD, Supervisor, or Inspector."];
        if (pageNumber < 1)
            errors[nameof(page)] = ["Page must be greater than zero."];
        if (size is < 1 or > 100)
            errors[nameof(pageSize)] = ["Page size must be between 1 and 100."];
        if (errors.Count > 0) return ApiErrors.Validation(errors);

        var query = from user in context.Users.AsNoTracking()
                    where context.UserRoles.Any(assignment => assignment.UserId == user.Id
                        && context.Roles.Any(candidate => candidate.Id == assignment.RoleId
                            && candidate.Name != AuthRoleCatalog.Admin
                            && candidate.Name != AuthRoleCatalog.DepartmentHead))
                    select user;
        if (active.HasValue) query = query.Where(user => user.IsActive == active.Value);
        if (normalizedRole is not null)
        {
            query = query.Where(user => context.UserRoles.Any(assignment => assignment.UserId == user.Id
                && context.Roles.Any(candidate => candidate.Id == assignment.RoleId
                    && candidate.Name == normalizedRole)));
        }
        if (normalizedSearch is not null)
        {
            query = query.Where(user => user.DisplayName.Contains(normalizedSearch)
                || (user.Email != null && user.Email.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(user => user.DisplayName).ThenBy(user => user.Email)
            .Skip((pageNumber - 1) * size).Take(size)
            .Select(user => new { user.Id, user.DisplayName, user.Email, user.IsActive })
            .ToListAsync(cancellationToken);
        var userIds = users.Select(user => user.Id).ToArray();
        var assignments = await (from assignment in context.UserRoles.AsNoTracking()
                                 join candidate in context.Roles.AsNoTracking()
                                     on assignment.RoleId equals candidate.Id
                                 where userIds.Contains(assignment.UserId)
                                     && candidate.Name != null
                                     && ManagedRoles.Contains(candidate.Name)
                                 select new { assignment.UserId, Role = candidate.Name! })
            .ToListAsync(cancellationToken);
        var roleByUser = assignments.GroupBy(item => item.UserId)
            .ToDictionary(grouping => grouping.Key,
                grouping => string.Join(", ", grouping.Select(item => item.Role)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)));
        var items = users.Where(user => roleByUser.ContainsKey(user.Id))
            .Select(user => new AdminAccountResponse(
                user.Id, user.DisplayName, user.Email ?? string.Empty,
                roleByUser[user.Id], user.IsActive))
            .ToArray();
        return Results.Ok(new AdminAccountPageResponse(totalCount, pageNumber, size, items));
    }

    private static async Task<IResult> CreateAccountAsync(
        AdminCreateAccountRequest request,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        if (!CreateableRoles.Contains(request.Role.Trim(), StringComparer.Ordinal))
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = ["Only Supervisor or Inspector accounts can be created here."]
            });

        var role = request.Role.Trim();
        var roleEntity = await roleManager.FindByNameAsync(role);
        if (roleEntity is null) return ApiErrors.Conflict("The selected role is not available.");

        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            IsActive = request.IsActive
        };
        var created = await userManager.CreateAsync(user, request.InitialPassword);
        if (!created.Succeeded) return IdentityFailure(created);
        var assigned = await userManager.AddToRoleAsync(user, role);
        if (!assigned.Succeeded) return IdentityFailure(assigned);
        await CommitAsync(transaction, cancellationToken);
        return Results.Created($"/api/v1/admin/accounts/{user.Id}",
            new AdminAccountResponse(user.Id, user.DisplayName, email, role, user.IsActive));
    }

    private static async Task<IResult> UpdateAccountAsync(
        Guid id,
        AdminUpdateAccountRequest request,
        ClaimsPrincipal principal,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RefreshSessionService refreshSessions,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        var role = request.Role.Trim();
        if (!CreateableRoles.Contains(role, StringComparer.Ordinal))
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = ["Role can only be Supervisor or Inspector. Use GSD-head replacement to change GSD access."]
            });

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || !await IsManagedAccountAsync(user, userManager))
            return ApiErrors.NotFound("Managed account not found.");
        if (TryGetActorId(principal, out var adminId) && adminId == id && !request.IsActive)
            return ApiErrors.Conflict("The signed-in Admin account must remain active.");

        var oldRoles = await userManager.GetRolesAsync(user);
        if (oldRoles.Contains(AuthRoleCatalog.Gsd, StringComparer.Ordinal))
            return ApiErrors.Conflict("Use GSD-head replacement to edit the active GSD account.");

        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);
        var oldEmail = user.Email;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.IsActive = request.IsActive;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityFailure(update);

        var currentManagedRole = oldRoles.FirstOrDefault(CreateableRoles.Contains);
        if (currentManagedRole is null)
            return ApiErrors.Conflict("The account no longer has a manageable role. Refresh the account list.");
        if (!string.Equals(currentManagedRole, role, StringComparison.Ordinal))
        {
            var removed = await userManager.RemoveFromRoleAsync(user, currentManagedRole);
            if (!removed.Succeeded) return IdentityFailure(removed);
            var added = await userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded) return IdentityFailure(added);
        }

        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return IdentityFailure(stamp);
        await CommitAsync(transaction, cancellationToken);
        await refreshSessions.RevokeAllForUserAsync(id, "Account changed", cancellationToken);
        return Results.Ok(new AdminAccountResponse(id, user.DisplayName, user.Email ?? oldEmail ?? "", role, user.IsActive));
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid id,
        AdminResetPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        RefreshSessionService refreshSessions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length > 256)
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.NewPassword)] = ["Password is required and must not exceed 256 characters."]
            });
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || !await IsManagedAccountAsync(user, userManager))
            return ApiErrors.NotFound("Managed account not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded) return IdentityFailure(result);
        await refreshSessions.RevokeAllForUserAsync(id, "Password reset by Admin", cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAccountAsync(
        Guid id,
        ClaimsPrincipal principal,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RefreshSessionService refreshSessions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(principal, out var adminId)) return Results.Forbid();
        if (adminId == id) return ApiErrors.Conflict("The signed-in Admin account must remain available.");
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || !await IsManagedAccountAsync(user, userManager))
            return ApiErrors.NotFound("Managed account not found.");
        if ((await userManager.GetRolesAsync(user)).Contains(AuthRoleCatalog.Gsd, StringComparer.Ordinal))
            return ApiErrors.Conflict("Replace the GSD head before deleting this account.");

        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);
        var currentAssignments = await context.PreventiveMaintenanceSchedules
            .Where(schedule => schedule.AssignedToUserId == id || schedule.AssignedSupervisorUserId == id)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var schedule in currentAssignments)
        {
            if (schedule.AssignedToUserId == id)
            {
                ScheduleAssignmentEventRecorder.RecordChange(
                    context,
                    schedule,
                    "Inspector",
                    id,
                    null,
                    adminId,
                    now,
                    "Account deleted",
                    hasPriorAssignmentHistory: true);
                schedule.AssignedToUserId = null;
            }

            if (schedule.AssignedSupervisorUserId == id)
            {
                ScheduleAssignmentEventRecorder.RecordChange(
                    context,
                    schedule,
                    "Supervisor",
                    id,
                    null,
                    adminId,
                    now,
                    "Account deleted",
                    hasPriorAssignmentHistory: true);
                schedule.AssignedSupervisorUserId = null;
            }

            schedule.UpdatedAt = now;
        }

        var retainedForHistory = currentAssignments.Count > 0
            || await HasHistoricalReferencesAsync(context, id, cancellationToken);
        if (retainedForHistory)
        {
            user.IsActive = false;
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded) return IdentityFailure(update);
            var stamp = await userManager.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded) return IdentityFailure(stamp);
        }
        else
        {
            var deletion = await userManager.DeleteAsync(user);
            if (!deletion.Succeeded) return IdentityFailure(deletion);
        }

        await context.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        await refreshSessions.RevokeAllForUserAsync(id, "Account deleted", cancellationToken);
        return Results.Ok(new AdminAccountDeleteResponse(
            true, retainedForHistory, currentAssignments.Count));
    }

    private static async Task<IResult> GetGsdHeadAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var accounts = await (from assignment in context.UserRoles.AsNoTracking()
                              join role in context.Roles.AsNoTracking()
                                  on assignment.RoleId equals role.Id
                              join user in context.Users.AsNoTracking()
                                  on assignment.UserId equals user.Id
                              where role.Name == AuthRoleCatalog.Gsd
                              orderby user.DisplayName, user.Email
                              select new AdminAccountResponse(
                                  user.Id, user.DisplayName, user.Email ?? string.Empty,
                                  AuthRoleCatalog.Gsd, user.IsActive))
            .ToListAsync(cancellationToken);
        var active = accounts.Where(account => account.IsActive).ToArray();
        return Results.Ok(new AdminGsdHeadResponse(
            active.Length == 1 ? active[0] : null,
            accounts,
            active.Length != 1));
    }

    private static async Task<IResult> UpdateGsdHeadAsync(
        AdminUpdateGsdHeadRequest request,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RefreshSessionService refreshSessions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        var currentHeads = await (from assignment in context.UserRoles
                                  join role in context.Roles on assignment.RoleId equals role.Id
                                  join candidate in context.Users on assignment.UserId equals candidate.Id
                                  where role.Name == AuthRoleCatalog.Gsd && candidate.IsActive
                                  select candidate).ToListAsync(cancellationToken);
        if (currentHeads.Count != 1)
            return ApiErrors.Conflict("Resolve the active GSD head before editing its details.");
        var user = currentHeads[0];
        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);
        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.EmailConfirmed = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityFailure(update);
        var stamp = await userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return IdentityFailure(stamp);
        await CommitAsync(transaction, cancellationToken);
        await refreshSessions.RevokeAllForUserAsync(user.Id, "GSD head profile changed", cancellationToken);
        return Results.Ok(new AdminAccountResponse(
            user.Id, user.DisplayName, user.Email ?? string.Empty, AuthRoleCatalog.Gsd, user.IsActive));
    }

    private static async Task<IResult> ReplaceGsdHeadAsync(
        AdminReplaceGsdHeadRequest request,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        RefreshSessionService refreshSessions,
        CancellationToken cancellationToken)
    {
        var requestErrors = request.Validate();
        if (requestErrors.Count > 0) return ApiErrors.Validation(requestErrors);
        await using var transaction = await BeginSerializableTransactionAsync(context, cancellationToken);
        var gsdRole = await roleManager.FindByNameAsync(AuthRoleCatalog.Gsd);
        if (gsdRole is null) return ApiErrors.Conflict("The GSD role is not available.");

        ApplicationUser replacement;
        var created = false;
        if (request.ReplacementUserId is Guid replacementId)
        {
            var existingReplacement = await userManager.FindByIdAsync(replacementId.ToString());
            if (existingReplacement is null) return ApiErrors.NotFound("Replacement account not found.");
            replacement = existingReplacement;
            if (!replacement.IsActive) return ApiErrors.Conflict("The replacement account must be active.");
            var replacementRoles = await userManager.GetRolesAsync(replacement);
            if (replacementRoles.Any(role => role == AuthRoleCatalog.Admin || role == AuthRoleCatalog.DepartmentHead)
                || !replacementRoles.Any(role => ManagedRoles.Contains(role, StringComparer.Ordinal)))
                return ApiErrors.Conflict("Choose an active Supervisor or Inspector account.");
        }
        else
        {
            var email = request.Email!.Trim();
            replacement = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email,
                EmailConfirmed = true, DisplayName = request.DisplayName!.Trim(), IsActive = true
            };
            var creation = await userManager.CreateAsync(replacement, request.InitialPassword!);
            if (!creation.Succeeded) return IdentityFailure(creation);
            created = true;
        }

        var targetRoles = await userManager.GetRolesAsync(replacement);
        var otherGsdUsers = await (from assignment in context.UserRoles
                                   join role in context.Roles on assignment.RoleId equals role.Id
                                   join user in context.Users on assignment.UserId equals user.Id
                                   where role.Name == AuthRoleCatalog.Gsd && user.Id != replacement.Id
                                   select user).Distinct().ToListAsync(cancellationToken);
        var changedUsers = new HashSet<Guid>();
        foreach (var other in otherGsdUsers)
        {
            var removed = await userManager.RemoveFromRoleAsync(other, AuthRoleCatalog.Gsd);
            if (!removed.Succeeded) return IdentityFailure(removed);
            var stamp = await userManager.UpdateSecurityStampAsync(other);
            if (!stamp.Succeeded) return IdentityFailure(stamp);
            changedUsers.Add(other.Id);
        }

        if (!targetRoles.Contains(AuthRoleCatalog.Gsd, StringComparer.Ordinal))
        {
            var removableRoles = targetRoles.Where(role => ManagedRoles.Contains(role, StringComparer.Ordinal)).ToArray();
            if (removableRoles.Length > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(replacement, removableRoles);
                if (!removed.Succeeded) return IdentityFailure(removed);
            }
            var added = await userManager.AddToRoleAsync(replacement, AuthRoleCatalog.Gsd);
            if (!added.Succeeded) return IdentityFailure(added);
            var stamp = await userManager.UpdateSecurityStampAsync(replacement);
            if (!stamp.Succeeded) return IdentityFailure(stamp);
            changedUsers.Add(replacement.Id);
        }

        await CommitAsync(transaction, cancellationToken);
        foreach (var userId in changedUsers)
            await refreshSessions.RevokeAllForUserAsync(userId, "GSD head changed", cancellationToken);

        return Results.Ok(new AdminGsdHeadResponse(
            new AdminAccountResponse(replacement.Id, replacement.DisplayName,
                replacement.Email ?? string.Empty, AuthRoleCatalog.Gsd, replacement.IsActive),
            [new AdminAccountResponse(replacement.Id, replacement.DisplayName,
                replacement.Email ?? string.Empty, AuthRoleCatalog.Gsd, replacement.IsActive)],
            false,
            created));
    }

    private static async Task<bool> IsManagedAccountAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Any(role => ManagedRoles.Contains(role, StringComparer.Ordinal));
    }

    private static async Task<bool> HasHistoricalReferencesAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.InspectionRecords.AnyAsync(row => row.InspectorUserId == userId, cancellationToken)
            || await context.PreventiveMaintenanceForms.AnyAsync(row => row.CreatedByUserId == userId
                || row.SubmittedByUserId == userId, cancellationToken)
            || await context.PreventiveMaintenanceAcknowledgements.AnyAsync(row => row.CapturedByUserId == userId, cancellationToken)
            || await context.ScheduleAssignmentReassignmentAudits.AnyAsync(row => row.PreviousAssigneeUserId == userId
                || row.NewAssigneeUserId == userId || row.ChangedByUserId == userId, cancellationToken)
            || await context.InspectionLocationAttempts.AnyAsync(row => row.ActorUserId == userId, cancellationToken)
            || await context.ScheduleEnrollmentDeferrals.AnyAsync(row => row.ReviewedByUserId == userId, cancellationToken)
            || await context.InspectionWmsReferrals.AnyAsync(row => row.RecordedByUserId == userId
                || row.LastUpdatedByUserId == userId, cancellationToken)
            || await context.InspectionWmsReferralAudits.AnyAsync(row => row.ChangedByUserId == userId, cancellationToken);
    }

    private static bool TryGetActorId(ClaimsPrincipal? principal, out Guid userId) =>
        Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);

    private static async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static async Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private static IResult IdentityFailure(IdentityResult result)
    {
        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            return ApiErrors.Conflict("An account with that login already exists.");
        var messages = result.Errors.Select(error => error.Description).Distinct().ToArray();
        return ApiErrors.Validation(new Dictionary<string, string[]>
        {
            ["account"] = messages.Length == 0 ? ["The account change was rejected."] : messages
        });
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminCreateAccountRequest(
    string DisplayName,
    string Email,
    string Role,
    string InitialPassword,
    bool IsActive = true)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Trim().Length > 160)
            errors[nameof(DisplayName)] = ["Display name is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(Email) || Email.Trim().Length > 256 || !Email.Contains('@'))
            errors[nameof(Email)] = ["A valid login email is required."];
        if (string.IsNullOrWhiteSpace(Role) || Role.Length > 32)
            errors[nameof(Role)] = ["Role is required."];
        if (string.IsNullOrWhiteSpace(InitialPassword) || InitialPassword.Length > 256)
            errors[nameof(InitialPassword)] = ["Initial password is required and must not exceed 256 characters."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateAccountRequest(string DisplayName, string Email, string Role, bool IsActive)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Trim().Length > 160)
            errors[nameof(DisplayName)] = ["Display name is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(Email) || Email.Trim().Length > 256 || !Email.Contains('@'))
            errors[nameof(Email)] = ["A valid login email is required."];
        if (string.IsNullOrWhiteSpace(Role) || Role.Length > 32)
            errors[nameof(Role)] = ["Role is required."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminResetPasswordRequest(string NewPassword);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateGsdHeadRequest(string DisplayName, string Email)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Trim().Length > 160)
            errors[nameof(DisplayName)] = ["Display name is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(Email) || Email.Trim().Length > 256 || !Email.Contains('@'))
            errors[nameof(Email)] = ["A valid login email is required."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminReplaceGsdHeadRequest(
    Guid? ReplacementUserId,
    string? DisplayName,
    string? Email,
    string? InitialPassword)
{
    internal Dictionary<string, string[]> Validate()
    {
        if (ReplacementUserId.HasValue)
        {
            if (DisplayName is null && Email is null && InitialPassword is null) return [];
            return new Dictionary<string, string[]>
            {
                [nameof(ReplacementUserId)] = ["Choose an existing account or provide new account details, not both."]
            };
        }
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Trim().Length > 160)
            errors[nameof(DisplayName)] = ["Display name is required and must not exceed 160 characters."];
        if (string.IsNullOrWhiteSpace(Email) || Email.Trim().Length > 256 || !Email.Contains('@'))
            errors[nameof(Email)] = ["A valid login email is required."];
        if (string.IsNullOrWhiteSpace(InitialPassword) || InitialPassword.Length > 256)
            errors[nameof(InitialPassword)] = ["Initial password is required and must not exceed 256 characters."];
        return errors;
    }
}

public sealed record AdminAccountResponse(Guid Id, string DisplayName, string Email, string Role, bool IsActive);
public sealed record AdminAccountPageResponse(int TotalCount, int Page, int PageSize, IReadOnlyList<AdminAccountResponse> Items);
public sealed record AdminAccountDeleteResponse(bool AccessRemoved, bool RetainedForHistory, int CurrentAssignmentsCleared);
public sealed record AdminGsdHeadResponse(
    AdminAccountResponse? ActiveHead,
    IReadOnlyList<AdminAccountResponse> Accounts,
    bool NeedsResolution,
    bool? ReplacementCreated = null);
