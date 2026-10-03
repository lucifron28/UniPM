using Microsoft.AspNetCore.Mvc;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Features.Reports;

public static class PmAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapPmAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/analytics")
            .WithTags("PM Analytics")
            .RequireAuthorization(policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthRoleCatalog.Gsd));

        group.MapPost("/pm/query", async (
            PmAnalyticsQuestionRequest request,
            PmAnalyticsService service,
            CancellationToken cancellationToken) =>
        {
            if (!PmAnalyticsQuestionParser.TryParse(
                    request.Question,
                    out var candidate,
                    out var parseError)
                || candidate is null)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.Question)] = [parseError]
                });
            }

            var execution = await service.ExecuteAsync(candidate, cancellationToken);
            if (execution.Response is null)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    ["plan"] = [execution.ValidationError ?? "The analytics plan is not supported."]
                });
            }

            return Results.Ok(execution.Response);
        })
        .WithName("QueryPmAnalytics")
        .WithSummary("Runs a deterministic query over supported preventive-maintenance metrics")
        .Produces<PmAnalyticsResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
