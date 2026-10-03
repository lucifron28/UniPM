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

        group.MapPost("/pm/interpret", async (
            PmAnalyticsInterpretationRequest request,
            INaturalLanguageAnalyticsInterpreter interpreter,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question)
                || request.Question.Length > 512
                || request.Question.Any(char.IsControl))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.Question)] = ["Question must be non-empty, at most 512 characters, and contain no control characters."]
                });
            }

            PmAnalyticsInterpretationResult result;
            try
            {
                result = await interpreter.InterpretAsync(request.Question, cancellationToken);
            }
            catch (NaturalLanguageAnalyticsProviderException exception)
            {
                var failure = exception.Failure switch
                {
                    NaturalLanguageAnalyticsProviderFailure.Timeout => "Timeout",
                    NaturalLanguageAnalyticsProviderFailure.InvalidOutput => "InvalidOutput",
                    _ => "ProviderUnavailable"
                };
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Natural language analytics is unavailable.",
                    detail: "The question could not be interpreted safely.",
                    extensions: new Dictionary<string, object?> { ["code"] = failure });
            }

            PmAnalyticsPlanResponse? planResponse = null;
            string? canonicalQuestion = null;
            if (result.Status == PmAnalyticsInterpretationStatus.Valid)
            {
                if (!PmAnalyticsPlanValidator.TryNormalize(result.Plan, out var plan, out _)
                    || !PmAnalyticsCanonicalQuestion.TryCreate(plan, out canonicalQuestion))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Natural language analytics is unavailable.",
                        detail: "The question could not be interpreted safely.",
                        extensions: new Dictionary<string, object?> { ["code"] = "InvalidOutput" });
                }

                planResponse = new PmAnalyticsPlanResponse(
                    plan.Metric.ToString(),
                    plan.AssetCategory,
                    plan.PmCycle,
                    plan.Department,
                    plan.GroupBy.ToString());
            }

            return Results.Ok(new PmAnalyticsInterpretationResponse(
                result.Status.ToString(),
                planResponse,
                result.ClarificationFields.Select(field => field.ToString()).ToArray(),
                result.Presentation?.ToString(),
                result.Code,
                canonicalQuestion));
        })
        .WithName("InterpretPmAnalyticsQuestion")
        .WithSummary("Interprets a preventive-maintenance analytics question without running it")
        .Produces<PmAnalyticsInterpretationResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
