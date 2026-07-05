using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Models;

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
                ScheduleDate = dto.ScheduleDate,
                PeriodType = dto.PeriodType.Trim(),
                Quarter = dto.Quarter?.Trim(),
                Year = dto.Year,
                Status = "Due",
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/schedules/{schedule.Id}", ScheduleResponse.FromSchedule(schedule));
        });

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
                var normalizedStatus = status.Trim().ToUpper();
                query = query.Where(schedule => schedule.Status.ToUpper() == normalizedStatus);
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
                var normalizedQuarter = quarter.Trim().ToUpper();
                query = query.Where(schedule => schedule.Quarter != null && schedule.Quarter.ToUpper() == normalizedQuarter);
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
        });

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
        });

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

        if (string.IsNullOrWhiteSpace(PeriodType))
        {
            errors.Add(nameof(PeriodType), ["Period type is required."]);
        }

        if (PeriodType.Contains("quarter", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(Quarter))
        {
            errors.Add(nameof(Quarter), ["Quarter is required for quarterly schedules."]);
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
