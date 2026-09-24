using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Models;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Features.Schedules;

public static class SchedulesEndpoints
{
    public static IEndpointRouteBuilder MapSchedulesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schedules").WithTags("Schedules");

        group.MapGet("/assignment-options", async (
            UserManager<ApplicationUser> userManager) =>
        {
            var workers = await userManager.GetUsersInRoleAsync(AuthRoleCatalog.Inspector);
            var supervisors = await userManager.GetUsersInRoleAsync(AuthRoleCatalog.Supervisor);
            return Results.Ok(new ScheduleAssignmentOptionsResponse(
                workers.Where(user => user.IsActive)
                    .OrderBy(user => user.DisplayName)
                    .Select(user => new ScheduleAssigneeOption(user.Id, user.DisplayName))
                    .ToArray(),
                supervisors.Where(user => user.IsActive)
                    .OrderBy(user => user.DisplayName)
                    .Select(user => new ScheduleAssigneeOption(user.Id, user.DisplayName))
                    .ToArray()));
        })
        .WithName("ListScheduleAssignmentOptions")
        .WithSummary("Lists active Inspector and Supervisor accounts for GSD batch assignment")
        .Produces<ScheduleAssignmentOptionsResponse>(StatusCodes.Status200OK)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleBatches);

        group.MapPost("/", async (
            CreateScheduleDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = dto.Validate();
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.FirstOrDefaultAsync(asset => asset.Id == dto.AssetId, cancellationToken);
            if (asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            var now = DateTimeOffset.UtcNow;

            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = dto.AssetId,
                Asset = asset,
                ScheduleDate = dto.ScheduleDate,
                PmCycle = PreventiveMaintenanceCycle.FromScheduleDate(dto.ScheduleDate),
                PeriodType = SchedulePeriodTypeCatalog.TryNormalize(dto.PeriodType, out var periodType)
                    ? periodType
                    : throw new InvalidOperationException("Validated schedule period type was not canonicalizable."),
                Quarter = ScheduleQuarterCatalog.TryNormalizeNullable(dto.Quarter, out var quarter)
                    ? quarter
                    : throw new InvalidOperationException("Validated schedule quarter was not canonicalizable."),
                Year = dto.Year,
                Status = ScheduleStatusCatalog.Due,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/schedules/{schedule.Id}", ScheduleResponse.FromSchedule(schedule));
        })
        .WithName("CreateSchedule")
        .WithSummary("Creates a preventive maintenance schedule for an existing asset")
        .Produces<ScheduleResponse>(StatusCodes.Status201Created)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules);

        group.MapPut("/{id:guid}/assignment", async (
            Guid id,
            AssignScheduleBatchDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var worker = await userManager.FindByIdAsync(dto.WorkerUserId.ToString());
            if (worker is null || !worker.IsActive
                || !await userManager.IsInRoleAsync(worker, AuthRoleCatalog.Inspector))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.WorkerUserId)] = ["Choose an active Inspector account."]
                });
            }

            var supervisor = await userManager.FindByIdAsync(dto.SupervisorUserId.ToString());
            if (supervisor is null || !supervisor.IsActive
                || !await userManager.IsInRoleAsync(supervisor, AuthRoleCatalog.Supervisor))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.SupervisorUserId)] = ["Choose an active Supervisor account."]
                });
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var selected = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Include(schedule => schedule.Asset)
                .SingleOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);
            if (selected?.Asset is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (selected.Status == ScheduleStatusCatalog.Cancelled)
            {
                return ApiErrors.Conflict("Cancelled schedules cannot be assigned.");
            }

            var department = selected.Asset.Department?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(department))
            {
                return ApiErrors.Conflict("Schedules without a department cannot be assigned as a batch.");
            }

            var pmCycle = PreventiveMaintenanceCycle.ForSchedule(selected);
            var batch = await context.PreventiveMaintenanceSchedules
                .Include(schedule => schedule.Asset)
                .Where(schedule => schedule.PmCycle == pmCycle
                    && schedule.Asset != null
                    && schedule.Asset.AssetCategory == selected.Asset.AssetCategory
                    && schedule.Asset.Department != null
                    && schedule.Asset.Department.ToUpper() == department
                    && schedule.Status != ScheduleStatusCatalog.Cancelled)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                return ApiErrors.Conflict("No active schedules were found in this PM batch.");
            }

            var eligibleStatuses = new[]
            {
                ScheduleStatusCatalog.Due,
                ScheduleStatusCatalog.Ongoing,
                ScheduleStatusCatalog.Overdue
            };
            if (batch.Any(schedule => !eligibleStatuses.Contains(schedule.Status)))
            {
                return ApiErrors.Conflict("A PM batch can only be assigned before its schedules are completed.");
            }

            var scheduleIds = batch.Select(schedule => schedule.Id).ToArray();
            if (await context.InspectionRecords.AnyAsync(
                    inspection => scheduleIds.Contains(inspection.ScheduleId),
                    cancellationToken))
            {
                return ApiErrors.Conflict("A PM batch cannot be reassigned after inspection work has started.");
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var schedule in batch)
            {
                schedule.AssignedToUserId = worker.Id;
                schedule.AssignedSupervisorUserId = supervisor.Id;
                schedule.UpdatedAt = now;
            }

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new ScheduleAssignmentBatchResponse(
                department,
                selected.Asset.AssetCategory,
                pmCycle,
                worker.Id,
                worker.DisplayName,
                supervisor.Id,
                supervisor.DisplayName,
                scheduleIds));
        })
        .WithName("AssignScheduleBatch")
        .WithSummary("Assigns the schedules in one department, category, and PM cycle batch")
        .Produces<ScheduleAssignmentBatchResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanAssignScheduleBatches);

        group.MapGet("/", async (
            Guid? assetId,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? quarter,
            int? year,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (from is not null && to is not null && from > to)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(from)] = ["From date must be earlier than or equal to to date."]
                });
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.PreventiveMaintenanceSchedules.AsNoTracking();

            if (assetId is not null)
            {
                query = query.Where(schedule => schedule.AssetId == assetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!ScheduleStatusCatalog.TryNormalize(status, out var normalizedStatus))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(status)] = ["Status must be a supported schedule status."]
                    });
                }

                query = query.Where(schedule => schedule.Status == normalizedStatus);
            }

            if (from is not null)
            {
                query = query.Where(schedule => schedule.ScheduleDate >= from.Value);
            }

            if (to is not null)
            {
                query = query.Where(schedule => schedule.ScheduleDate <= to.Value);
            }

            if (!string.IsNullOrWhiteSpace(quarter))
            {
                if (!ScheduleQuarterCatalog.TryNormalizeNullable(quarter, out var normalizedQuarter))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(quarter)] = ["Quarter must be one of Q1, Q2, Q3, or Q4."]
                    });
                }

                query = query.Where(schedule => schedule.Quarter == normalizedQuarter);
            }

            if (year is not null)
            {
                query = query.Where(schedule => schedule.Year == year.Value);
            }

            var schedules = await query
                .OrderBy(schedule => schedule.ScheduleDate)
                .ThenBy(schedule => schedule.Id)
                .Select(schedule => new ScheduleResponse(
                    schedule.Id,
                    schedule.AssetId,
                    schedule.ScheduleDate,
                    schedule.PmCycle,
                    schedule.PeriodType,
                    schedule.Status,
                    schedule.Quarter,
                    schedule.Semester,
                    schedule.Year,
                    schedule.AcademicYear,
                    schedule.AssignedToUserId,
                    schedule.AssignedSupervisorUserId,
                    schedule.CompletedAt,
                    schedule.CreatedAt,
                    schedule.UpdatedAt,
                    schedule.Asset == null
                        ? null
                        : new ScheduleAssetResponse(
                            schedule.Asset.Id,
                            schedule.Asset.AssetCode,
                            schedule.Asset.AssetCategory,
                            schedule.Asset.Building,
                            schedule.Asset.Department,
                            schedule.Asset.Location)))
                .ToListAsync(cancellationToken);

            return Results.Ok(schedules);
        })
        .WithName("ListSchedules")
        .WithSummary("Lists preventive maintenance schedules using supported filters")
        .Produces<IReadOnlyList<ScheduleResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var schedule = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Include(schedule => schedule.Asset)
                .FirstOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

            return schedule is not null
                ? Results.Ok(ScheduleResponse.FromSchedule(schedule))
                : ApiErrors.NotFound("Schedule not found.");
        })
        .WithName("GetSchedule")
        .WithSummary("Gets a preventive maintenance schedule by its identifier")
        .Produces<ScheduleResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

public sealed record ScheduleResponse(
    Guid Id,
    Guid AssetId,
    DateTimeOffset ScheduleDate,
    string PmCycle,
    string PeriodType,
    string Status,
    string? Quarter,
    string? Semester,
    int? Year,
    string? AcademicYear,
    Guid? AssignedToUserId,
    Guid? AssignedSupervisorUserId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ScheduleAssetResponse? Asset)
{
    internal static ScheduleResponse FromSchedule(PreventiveMaintenanceSchedule schedule)
    {
        return new ScheduleResponse(
            schedule.Id,
            schedule.AssetId,
            schedule.ScheduleDate,
            PreventiveMaintenanceCycle.ForSchedule(schedule),
            schedule.PeriodType,
            schedule.Status,
            schedule.Quarter,
            schedule.Semester,
            schedule.Year,
            schedule.AcademicYear,
            schedule.AssignedToUserId,
            schedule.AssignedSupervisorUserId,
            schedule.CompletedAt,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            schedule.Asset is null
                ? null
                : new ScheduleAssetResponse(
                    schedule.Asset.Id,
                    schedule.Asset.AssetCode,
                    schedule.Asset.AssetCategory,
                    schedule.Asset.Building,
                    schedule.Asset.Department,
                    schedule.Asset.Location));
    }
}

public sealed record ScheduleAssigneeOption(Guid Id, string DisplayName);

public sealed record ScheduleAssignmentOptionsResponse(
    IReadOnlyList<ScheduleAssigneeOption> Workers,
    IReadOnlyList<ScheduleAssigneeOption> Supervisors);

public sealed record ScheduleAssignmentBatchResponse(
    string Department,
    string AssetCategory,
    string PmCycle,
    Guid WorkerUserId,
    string WorkerDisplayName,
    Guid SupervisorUserId,
    string SupervisorDisplayName,
    IReadOnlyList<Guid> ScheduleIds);

public sealed class AssignScheduleBatchDto
{
    public Guid WorkerUserId { get; set; }
    public Guid SupervisorUserId { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (WorkerUserId == Guid.Empty)
        {
            errors[nameof(WorkerUserId)] = ["An Inspector must be assigned."];
        }

        if (SupervisorUserId == Guid.Empty)
        {
            errors[nameof(SupervisorUserId)] = ["A Supervisor must be assigned."];
        }

        return errors;
    }
}

public sealed record ScheduleAssetResponse(
    Guid Id,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location);

public class CreateScheduleDto
{
    public Guid AssetId { get; set; }
    public DateTimeOffset ScheduleDate { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string? Quarter { get; set; }
    public int? Year { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (AssetId == Guid.Empty)
        {
            errors.Add(nameof(AssetId), ["Asset ID is required."]);
        }

        if (ScheduleDate == default)
        {
            errors.Add(nameof(ScheduleDate), ["Schedule date is required."]);
        }

        var hasSupportedPeriodType = SchedulePeriodTypeCatalog.TryNormalize(PeriodType, out var normalizedPeriodType);
        if (string.IsNullOrWhiteSpace(PeriodType))
        {
            errors.Add(nameof(PeriodType), ["Period type is required."]);
        }
        else if (!hasSupportedPeriodType)
        {
            errors.Add(nameof(PeriodType), ["Period type must be a supported maintenance period."]);
        }

        if (hasSupportedPeriodType
            && string.Equals(normalizedPeriodType, SchedulePeriodTypeCatalog.Quarter, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(Quarter))
        {
            errors.Add(nameof(Quarter), ["Quarter is required for quarterly schedules."]);
        }

        if (!ScheduleQuarterCatalog.TryNormalizeNullable(Quarter, out _))
        {
            errors.Add(nameof(Quarter), ["Quarter must be one of Q1, Q2, Q3, or Q4."]);
        }

        if (!string.IsNullOrWhiteSpace(PeriodType) && PeriodType.Trim().Length > 32)
        {
            errors.Add(nameof(PeriodType), ["Period type must not exceed 32 characters."]);
        }

        if (Year is not null)
        {
            var maxPlanningYear = DateTimeOffset.UtcNow.Year + 5;
            if (Year < 2000 || Year > maxPlanningYear)
            {
                errors.Add(nameof(Year), [$"Year must be between 2000 and {maxPlanningYear}."]);
            }
        }

        return errors;
    }
}
