using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionsEndpoints
{
    public static IEndpointRouteBuilder MapInspectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inspections").WithTags("Inspections");

        group.MapPost("/", async (
            RecordInspectionDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            MaintenanceSearchDocumentProjector projector,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = dto.Validate();
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var schedule = await context.PreventiveMaintenanceSchedules
                .FirstOrDefaultAsync(schedule => schedule.Id == dto.ScheduleId, cancellationToken);

            if (schedule is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            var asset = await context.Assets
                .FirstOrDefaultAsync(candidate => candidate.Id == schedule.AssetId, cancellationToken);

            if (asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            var scheduleAlreadyInspected = await context.InspectionRecords
                .AnyAsync(inspection => inspection.ScheduleId == dto.ScheduleId, cancellationToken);

            if (scheduleAlreadyInspected)
            {
                return ApiErrors.Conflict("Schedule already has a recorded inspection.");
            }

            var now = DateTimeOffset.UtcNow;

            var inspection = new InspectionRecord
            {
                Id = Guid.NewGuid(),
                ScheduleId = dto.ScheduleId,
                AssetId = schedule.AssetId,
                InspectorUserId = dto.InspectorUserId,
                DateInspected = dto.DateInspected,
                IsOperational = dto.IsOperational,
                Remarks = dto.Remarks?.Trim(),
                ActionsRecommendations = dto.ActionsRecommendations?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            schedule.Status = ScheduleStatusCatalog.Completed;
            schedule.CompletedAt = now;
            schedule.UpdatedAt = now;

            context.InspectionRecords.Add(inspection);
            context.MaintenanceSearchDocuments.Add(projector.Build(inspection, asset));
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/v1/inspections/{inspection.Id}",
                InspectionResponse.FromInspection(inspection));
        });

        group.MapGet("/history/{assetId}", async (
            Guid assetId,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var history = await context.InspectionRecords
                .Where(i => i.AssetId == assetId)
                .OrderByDescending(i => i.DateInspected)
                .Select(i => new
                {
                    i.Id,
                    i.DateInspected,
                    i.IsOperational,
                    i.Remarks,
                    i.ActionsRecommendations
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(history);
        });

        group.MapGet("/", async (
            Guid? assetId,
            Guid? scheduleId,
            bool? isOperational,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dateFrom)] = ["Date from must be earlier than or equal to date to."]
                });
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.InspectionRecords
                .AsNoTracking()
                .AsQueryable();

            if (assetId is not null)
            {
                query = query.Where(inspection => inspection.AssetId == assetId.Value);
            }

            if (scheduleId is not null)
            {
                query = query.Where(inspection => inspection.ScheduleId == scheduleId.Value);
            }

            if (isOperational is not null)
            {
                query = query.Where(inspection => inspection.IsOperational == isOperational.Value);
            }

            if (dateFrom is not null)
            {
                query = query.Where(inspection => inspection.DateInspected >= dateFrom.Value);
            }

            if (dateTo is not null)
            {
                query = query.Where(inspection => inspection.DateInspected <= dateTo.Value);
            }

            var inspections = await query
                .OrderByDescending(inspection => inspection.DateInspected)
                .ThenBy(inspection => inspection.Id)
                .Select(inspection => new InspectionResponse(
                    inspection.Id,
                    inspection.ScheduleId,
                    inspection.AssetId,
                    inspection.InspectorUserId,
                    inspection.DateInspected,
                    inspection.IsOperational,
                    inspection.Remarks,
                    inspection.ActionsRecommendations,
                    inspection.CreatedAt,
                    inspection.UpdatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(inspections);
        });

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            return inspection is not null
                ? Results.Ok(InspectionResponse.FromInspection(inspection))
                : ApiErrors.NotFound("Inspection not found.");
        });

        return endpoints;
    }
}

public sealed record InspectionResponse(
    Guid Id,
    Guid ScheduleId,
    Guid AssetId,
    Guid InspectorUserId,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static InspectionResponse FromInspection(InspectionRecord inspection)
    {
        return new InspectionResponse(
            inspection.Id,
            inspection.ScheduleId,
            inspection.AssetId,
            inspection.InspectorUserId,
            inspection.DateInspected,
            inspection.IsOperational,
            inspection.Remarks,
            inspection.ActionsRecommendations,
            inspection.CreatedAt,
            inspection.UpdatedAt);
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

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (ScheduleId == Guid.Empty)
        {
            errors.Add(nameof(ScheduleId), ["Schedule ID is required."]);
        }

        if (InspectorUserId == Guid.Empty)
        {
            errors.Add(nameof(InspectorUserId), ["Inspector user ID is required."]);
        }

        if (DateInspected == default)
        {
            errors.Add(nameof(DateInspected), ["Date inspected is required."]);
        }
        else if (DateInspected > DateTimeOffset.UtcNow.AddDays(1))
        {
            errors.Add(nameof(DateInspected), ["Date inspected cannot be more than one day in the future."]);
        }

        if (Remarks?.Length > 2_000)
        {
            errors.Add(nameof(Remarks), ["Remarks must be 2,000 characters or fewer."]);
        }

        if (ActionsRecommendations?.Length > 2_000)
        {
            errors.Add(nameof(ActionsRecommendations), ["Actions and recommendations must be 2,000 characters or fewer."]);
        }

        return errors;
    }
}
