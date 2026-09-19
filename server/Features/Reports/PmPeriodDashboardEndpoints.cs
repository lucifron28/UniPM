using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Reports;

public static class PmPeriodDashboardEndpoints
{
    public static IEndpointRouteBuilder MapPmPeriodDashboardEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/pm-period-dashboard")
            .WithTags("PM Period Dashboard")
            .RequireAuthorization();

        group.MapGet("/cycles", async (
            string? assetCategory,
            int? year,
            PmPeriodDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            string? normalizedCategory = null;
            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var parsedCategory))
                {
                    errors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
                }
                else
                {
                    normalizedCategory = parsedCategory;
                }
            }

            if (year is <= 0)
            {
                errors[nameof(year)] = ["Year must be a positive number."];
            }

            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var cycles = await service.GetAvailableCyclesAsync(
                normalizedCategory,
                year,
                cancellationToken);
            return Results.Ok(cycles);
        })
        .WithName("ListPmPeriodDashboardCycles")
        .WithSummary("Lists available PM cycles grouped by asset category and year")
        .Produces<IReadOnlyList<PmPeriodDashboardCycleGroupResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
            string? pmCycle,
            string? assetCategory,
            string? department,
            string? condition,
            string? timeliness,
            string? search,
            PmPeriodDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!PreventiveMaintenanceCycle.TryParse(pmCycle, out _, out _))
            {
                errors[nameof(pmCycle)] = ["PM cycle must use the yyyy-MM format."];
            }

            if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var normalizedCategory))
            {
                errors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
            }

            string? normalizedCondition = null;
            if (!string.IsNullOrWhiteSpace(condition))
            {
                if (!PmPeriodDashboardFilterCatalog.TryNormalizeCondition(condition, out var parsedCondition))
                {
                    errors[nameof(condition)] = ["Condition must be Operational, NonOperational, or NotInspected."];
                }
                else
                {
                    normalizedCondition = parsedCondition;
                }
            }

            string? normalizedTimeliness = null;
            if (!string.IsNullOrWhiteSpace(timeliness))
            {
                if (!PmPeriodDashboardFilterCatalog.TryNormalizeTimeliness(timeliness, out var parsedTimeliness))
                {
                    errors[nameof(timeliness)] = ["Timeliness must be OnTime, Late, or NotCompleted."];
                }
                else
                {
                    normalizedTimeliness = parsedTimeliness;
                }
            }

            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim().ToUpperInvariant();
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (normalizedSearch?.Length > 256)
            {
                errors[nameof(search)] = ["Search must not exceed 256 characters."];
            }

            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var response = await service.GetPeriodAsync(
                new PmPeriodDashboardQuery(
                    pmCycle!.Trim(),
                    normalizedCategory,
                    normalizedDepartment,
                    normalizedCondition,
                    normalizedTimeliness,
                    normalizedSearch),
                cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetPmPeriodDashboard")
        .WithSummary("Gets a filtered PM-period dashboard read model")
        .Produces<PmPeriodDashboardResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
