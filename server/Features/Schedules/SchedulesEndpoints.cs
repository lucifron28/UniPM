using Microsoft.EntityFrameworkCore;
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
                    schedule.PeriodType,
                    schedule.Status,
                    schedule.Quarter,
                    schedule.Semester,
                    schedule.Year,
                    schedule.AcademicYear,
                    schedule.AssignedToUserId,
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
    string PeriodType,
    string Status,
    string? Quarter,
    string? Semester,
    int? Year,
    string? AcademicYear,
    Guid? AssignedToUserId,
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
            schedule.PeriodType,
            schedule.Status,
            schedule.Quarter,
            schedule.Semester,
            schedule.Year,
            schedule.AcademicYear,
            schedule.AssignedToUserId,
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
