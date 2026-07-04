using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionsEndpoints
{
    public static IEndpointRouteBuilder MapInspectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inspections").WithTags("Inspections");

        group.MapPost("/", async (RecordInspectionDto dto, IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var context = await factory.CreateDbContextAsync();
            var schedule = await context.PreventiveMaintenanceSchedules.FindAsync(dto.ScheduleId);
            if (schedule is null) return Results.NotFound(new { message = "Schedule not found" });

            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = dto.ScheduleId,
                AssetId = schedule.AssetId,
                InspectorUserId = dto.InspectorUserId,
                DateInspected = dto.DateInspected,
                IsOperational = dto.IsOperational,
                Remarks = dto.Remarks,
                ActionsRecommendations = dto.ActionsRecommendations,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            schedule.Status = "Completed";
            schedule.CompletedAt = DateTimeOffset.UtcNow;
            schedule.UpdatedAt = DateTimeOffset.UtcNow;

            context.InspectionRecords.Add(inspection);
            await context.SaveChangesAsync();

            return Results.Created($"/api/v1/inspections/{inspection.Id}", inspection);
        });

        group.MapGet("/history/{assetId}", async (Guid assetId, IDbContextFactory<ApplicationDbContext> factory) =>
        {
            await using var context = await factory.CreateDbContextAsync();
            var history = await context.InspectionRecords
                .Where(i => i.AssetId == assetId)
                .OrderByDescending(i => i.DateInspected)
                .Select(i => new {
                    i.Id,
                    i.DateInspected,
                    i.IsOperational,
                    i.Remarks,
                    i.ActionsRecommendations
                })
                .ToListAsync();

            return Results.Ok(history);
        });

        return endpoints;
    }
}

public class RecordInspectionDto
{
    public Guid ScheduleId { get; set; }
    public Guid InspectorUserId { get; set; }
    public DateTimeOffset DateInspected { get; set; }
    public bool IsOperational { get; set; }
    public string? Remarks { get; set; }
    public string? ActionsRecommendations { get; set; }
}
