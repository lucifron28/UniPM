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

        group.MapPost("/pm/v2/interpret", async (
            PmAnalyticsV2InterpretRequest request,
            PmAnalyticsV2QuestionInterpreter interpreter,
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

            var response = await interpreter.InterpretAsync(request.Question, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("InterpretPmAnalyticsV2Question")
        .WithSummary("Interprets a bounded preventive-maintenance analytics question using the V2 deterministic grammar")
        .Produces<PmAnalyticsV2InterpretationResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/pm/v2/query", async (
            PmAnalyticsV2QueryRequest request,
            PmAnalyticsV2QueryService service,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (!Enum.IsDefined(request.SourceScope))
            {
                errors[nameof(request.SourceScope)] = ["SourceScope must be Matching or Calculation."];
            }

            if (request.Page is < 1 or > 100_000)
            {
                errors[nameof(request.Page)] = ["Page must be between 1 and 100000."];
            }

            if (request.PageSize is < 1 or > 100)
            {
                errors[nameof(request.PageSize)] = ["PageSize must be between 1 and 100."];
            }

            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var resolution = await service.NormalizeAndResolveAsync(request.Plan, cancellationToken);
            if (resolution.Plan is null)
            {
                if (resolution.Errors.Count == 0)
                {
                    resolution.Errors["plan"] = ["The analytics plan is incomplete or unsupported."];
                }

                return ApiErrors.Validation(resolution.Errors);
            }

            var response = await service.ExecuteAsync(
                resolution.Plan,
                request.SourceScope,
                request.Page,
                request.PageSize,
                cancellationToken);
            return Results.Ok(response);
        })
        .WithName("QueryPmAnalyticsV2")
        .WithSummary("Executes a validated typed V2 preventive-maintenance analytics plan")
        .Produces<PmAnalyticsV2QueryResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

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
