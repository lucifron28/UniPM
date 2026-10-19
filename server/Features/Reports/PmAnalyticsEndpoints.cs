using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.ReferenceData;

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

        group.MapGet("/pm/v3/catalog", () => Results.Ok(PmAnalyticsV3QueryService.CreateCatalogResponse()))
            .WithName("GetPmAnalyticsV3Catalog")
            .WithSummary("Returns the versioned PM execution measure, dimension and field catalog")
            .Produces<PmAnalyticsV3CatalogResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/pm/v3/catalog/values", async (
            [FromQuery] string? field,
            [FromQuery] string[]? categories,
            [FromQuery] string? search,
            PmAnalyticsV3QueryService service,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            var definition = PmAnalyticsV3Catalog.FindFilterField(field);
            if (definition is null || !definition.IsBaseFilter
                || definition.ValueType is not ("string"))
            {
                errors[nameof(field)] = ["Field must be a catalogued string population filter."];
            }

            var normalizedCategories = new List<string>();
            if (categories is null || categories.Length is < 1 or > 4)
            {
                errors[nameof(categories)] = ["Select between one and four supported asset categories."];
            }
            else
            {
                foreach (var rawCategory in categories)
                {
                    if (!AssetCategoryCatalog.TryNormalize(rawCategory, out var category)
                        || !PmAnalyticsV3Catalog.IsCategory(category))
                    {
                        errors[nameof(categories)] = ["Every category must be one of the supported CPMP categories."];
                        break;
                    }

                    if (!normalizedCategories.Contains(category, StringComparer.Ordinal))
                    {
                        normalizedCategories.Add(category);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length is < 2 or > 128
                || search.Any(char.IsControl))
            {
                errors[nameof(search)] = ["Search must contain between 2 and 128 characters and no control characters."];
            }

            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            var response = await service.FindFilterValuesAsync(
                field!,
                normalizedCategories,
                search!,
                cancellationToken);
            return Results.Ok(response);
        })
        .WithName("GetPmAnalyticsV3FilterValues")
        .WithSummary("Searches current asset catalog values for a V3 population filter")
        .Produces<PmAnalyticsV3FilterValuesResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

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

        group.MapPost("/pm/v3/preview", async (
            PmAnalyticsV3PreviewRequest request,
            PmAnalyticsV3QueryService service,
            PmAnalyticsV3PreviewStore previewStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var resolution = await service.NormalizeAndResolveAsync(request.Plan, cancellationToken);
            if (resolution.Plan is null)
            {
                if (resolution.Errors.Count == 0)
                {
                    resolution.Errors["plan"] = ["The report plan is incomplete or unsupported."];
                }

                return ApiErrors.Validation(resolution.Errors);
            }

            var userId = GetUserId(httpContext.User);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (!previewStore.TryCreate(userId, resolution.Plan, out var previewId, out var expiresAt))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "A report preview could not be created.",
                    detail: "Try creating the preview again.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PreviewUnavailable" });
            }

            return Results.Ok(new PmAnalyticsV3PreviewResponse(
                previewId,
                expiresAt,
                resolution.Plan.Plan,
                resolution.Plan.CyclesByCategory,
                PmAnalyticsV3QueryService.CreatePreviewSummary(resolution.Plan),
                PmAnalyticsV3QueryService.ScopeNotes));
        })
        .WithName("PreviewPmAnalyticsV3Report")
        .WithSummary("Validates a V3 report plan and creates a short-lived user-bound preview")
        .Produces<PmAnalyticsV3PreviewResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/pm/v3/interpret", async (
            PmAnalyticsV3InterpretRequest request,
            PmAnalyticsV3QuestionInterpreter interpreter,
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

            try
            {
                var response = await interpreter.InterpretAsync(request.Question, cancellationToken);
                return Results.Ok(response);
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
                    title: "V3 report interpretation is unavailable.",
                    detail: "The question could not be interpreted safely.",
                    extensions: new Dictionary<string, object?> { ["code"] = failure });
            }
        })
        .WithName("InterpretPmAnalyticsV3Question")
        .WithSummary("Interprets a PM execution question into a V3 report plan without running it")
        .Produces<PmAnalyticsV3InterpretationResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/pm/v3/execute", async (
            PmAnalyticsV3ExecuteRequest request,
            PmAnalyticsV3QueryService service,
            PmAnalyticsV3PreviewStore previewStore,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var requestErrors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.PreviewId)
                || request.PreviewId.Length > PmAnalyticsV3PreviewStore.MaximumTokenLength)
            {
                requestErrors[nameof(request.PreviewId)] = ["PreviewId is required and must be valid."];
            }

            if (request.Page is < 1 or > PmAnalyticsV3QueryService.MaximumPage)
            {
                requestErrors[nameof(request.Page)] = [$"Page must be between 1 and {PmAnalyticsV3QueryService.MaximumPage}."];
            }

            if (request.PageSize is < 1 or > PmAnalyticsV3QueryService.MaximumPageSize)
            {
                requestErrors[nameof(request.PageSize)] = [$"PageSize must be between 1 and {PmAnalyticsV3QueryService.MaximumPageSize}."];
            }

            if (requestErrors.Count > 0)
            {
                return ApiErrors.Validation(requestErrors);
            }

            var userId = GetUserId(httpContext.User);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (previewStore.TryGet(userId, request.PreviewId, out var storedPlan)
                != PmAnalyticsV3PreviewLookup.Found
                || storedPlan is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The report preview is unavailable.",
                    detail: "Create a new preview and confirm it before execution.",
                    extensions: new Dictionary<string, object?> { ["code"] = "PreviewUnavailable" });
            }

            var resolution = await service.NormalizeAndResolveAsync(storedPlan.Plan, cancellationToken);
            if (resolution.Plan is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The report preview is stale.",
                    detail: "The catalog or selected values changed. Create and review a new preview.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "PreviewStale",
                        ["errors"] = resolution.Errors
                    });
            }

            try
            {
                var response = await service.ExecuteAsync(
                    resolution.Plan,
                    request.Page,
                    request.PageSize,
                    cancellationToken);
                return Results.Ok(response);
            }
            catch (PmAnalyticsV3ExecutionLimitException exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "The report exceeds the current execution limit.",
                    detail: exception.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = "ExecutionLimitExceeded" });
            }
            catch (PmAnalyticsV3UnmeasurableThresholdException exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "The aggregate filter cannot be fully evaluated.",
                    detail: "At least one group has an unmeasurable value for the requested threshold. Choose a closed period or remove that threshold.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "UnmeasurableThreshold",
                        ["measure"] = exception.Measure,
                        ["groups"] = exception.Groups,
                        ["totalGroupCount"] = exception.TotalGroupCount
                    });
            }
        })
        .WithName("ExecutePmAnalyticsV3Report")
        .WithSummary("Revalidates and executes a confirmed V3 PM execution report")
        .Produces<PmAnalyticsV3ExecutionResponse>(StatusCodes.Status200OK)
        .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity);

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

    private static string? GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub");
}
