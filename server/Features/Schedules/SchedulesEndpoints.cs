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

            return Results.Created($"/api/v1/schedules/{schedule.Id}", schedule);
        });

        return endpoints;
    }
}

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
