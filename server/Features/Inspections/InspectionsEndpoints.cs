using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.ReferenceData;
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
                    i.ActionsRecommendations,
                    i.DateAccomplished,
                    i.WaterReplaceCarbonFilter,
                    i.WaterReplaceSedimentFilter,
                    i.WaterCheckUvLight))
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
            string? department,
            string? assetCategory,
            string? search,
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

            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim();
            var normalizedCategory = string.Empty;
            var validationErrors = new Dictionary<string, string[]>();
            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (normalizedSearch?.Length > 256)
            {
                validationErrors[nameof(search)] = ["Search must be 256 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(assetCategory)
                && !AssetCategoryCatalog.TryNormalize(assetCategory, out normalizedCategory))
            {
                validationErrors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
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

            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(inspection =>
                    inspection.Asset != null
                    && inspection.Asset.Department != null
                    && inspection.Asset.Department.ToUpper() == departmentKey);
            }

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                query = query.Where(inspection =>
                    inspection.Asset != null
                    && inspection.Asset.AssetCategory == normalizedCategory);
            }

            if (normalizedSearch is not null)
            {
                var searchKey = normalizedSearch.ToUpperInvariant();
                query = query.Where(inspection =>
                    (inspection.Asset != null && (
                        inspection.Asset.AssetCode.Contains(searchKey)
                        || inspection.Asset.AssetCategory.ToUpper().Contains(searchKey)
                        || (inspection.Asset.Building != null && inspection.Asset.Building.ToUpper().Contains(searchKey))
                        || (inspection.Asset.Department != null && inspection.Asset.Department.ToUpper().Contains(searchKey))
                        || (inspection.Asset.Location != null && inspection.Asset.Location.ToUpper().Contains(searchKey))))
                    || (inspection.Remarks != null && inspection.Remarks.ToUpper().Contains(searchKey))
                    || (inspection.ActionsRecommendations != null && inspection.ActionsRecommendations.ToUpper().Contains(searchKey)));
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
                    inspection.UpdatedAt,
                    inspection.DateAccomplished,
                    inspection.WaterReplaceCarbonFilter,
                    inspection.WaterReplaceSedimentFilter,
                    inspection.WaterCheckUvLight))
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
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DateAccomplished = null,
    bool? WaterReplaceCarbonFilter = null,
    bool? WaterReplaceSedimentFilter = null,
    bool? WaterCheckUvLight = null)
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
            inspection.UpdatedAt,
            inspection.DateAccomplished,
            inspection.WaterReplaceCarbonFilter,
            inspection.WaterReplaceSedimentFilter,
            inspection.WaterCheckUvLight);
    }
}

public sealed record InspectionHistoryResponse(
    Guid Id,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations,
    DateTimeOffset? DateAccomplished = null,
    bool? WaterReplaceCarbonFilter = null,
    bool? WaterReplaceSedimentFilter = null,
    bool? WaterCheckUvLight = null);
