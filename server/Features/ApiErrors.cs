namespace UniPM.Api.Features;

internal static class ApiErrors
{
    internal static IResult Validation(IDictionary<string, string[]> errors)
    {
        return Results.ValidationProblem(
            errors,
            title: "Request validation failed",
            detail: "One or more request fields are invalid.");
    }

    internal static IResult NotFound(string detail)
    {
        return Results.Problem(
            title: "Resource not found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound);
    }

    internal static IResult Conflict(string detail)
    {
        return Results.Problem(
            title: "Resource conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
    }

    internal static IResult Unauthorized(string detail)
    {
        return Results.Problem(
            title: "Authentication failed",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized);
    }

    internal static IResult Forbidden(string detail)
    {
        return Results.Problem(
            title: "Request forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden);
    }

    internal static IResult ServiceUnavailable(string detail)
    {
        return Results.Problem(
            title: "Service unavailable",
            detail: detail,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    internal static IResult InternalFailure(string detail)
    {
        return Results.Problem(
            title: "Request could not be completed",
            detail: detail,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
