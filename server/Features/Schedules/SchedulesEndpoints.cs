using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

public static class SchedulesEndpoints
{
    public static IEndpointRouteBuilder MapSchedulesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schedules").WithTags("Schedules");

        group.MapPost("/", async (CreateScheduleDto dto, IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var context = await factory.CreateDbContextAsync();
            var asset = await context.Assets.FindAsync(dto.AssetId);
            if (asset is null) return Results.NotFound(new { message = "Asset not found" });

            var schedule = new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = dto.AssetId,
                ScheduleDate = dto.ScheduleDate,
                PeriodType = dto.PeriodType,
                Quarter = dto.Quarter,
                Year = dto.Year,
                Status = "Due",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            context.PreventiveMaintenanceSchedules.Add(schedule);
            await context.SaveChangesAsync();

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
}
