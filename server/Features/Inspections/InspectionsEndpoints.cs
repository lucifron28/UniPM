using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionsEndpoints
{
    public static IEndpointRouteBuilder MapInspectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inspections").WithTags("Inspections");

        group.MapGet("/history/{assetId}", async (
            Guid assetId,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var history = await context.InspectionRecords
                .WhereOfficial()
                .Where(i => i.AssetId == assetId)
                .OrderByDescending(i => i.DateInspected)
                .Select(i => new InspectionHistoryResponse(
                    i.Id,
                    i.DateInspected,
                    i.IsOperational,
                    i.Remarks,
                    i.ActionsRecommendations))
                .ToListAsync(cancellationToken);

            return Results.Ok(history);
        })
        .WithName("GetInspectionHistory")
        .WithSummary("Gets inspection history for an asset")
        .Produces<List<InspectionHistoryResponse>>(StatusCodes.Status200OK);

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
                .WhereOfficial()
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
        })
        .WithName("ListInspections")
        .WithSummary("Lists inspection records using supported metadata filters")
        .Produces<List<InspectionResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .AsNoTracking()
                .WhereOfficial()
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            return inspection is not null
                ? Results.Ok(InspectionResponse.FromInspection(inspection))
                : ApiErrors.NotFound("Inspection not found.");
        })
        .WithName("GetInspection")
        .WithSummary("Gets an inspection record by its identifier")
        .Produces<InspectionResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

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

public sealed record InspectionHistoryResponse(
    Guid Id,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations);
