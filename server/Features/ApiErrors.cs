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
}
