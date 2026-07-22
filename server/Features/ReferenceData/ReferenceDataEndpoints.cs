namespace UniPM.Api.Features.ReferenceData;

using UniPM.Api.Features.Schedules;

public static class ReferenceDataEndpoints
{
    public static RouteGroupBuilder MapReferenceDataEndpoints(this RouteGroupBuilder api)
    {
        var group = api
            .MapGroup("/reference-data")
            .WithTags("Reference Data");

        group
            .MapGet("/asset-categories", () => TypedResults.Ok(AssetCategoryCatalog.All))
            .WithName("ListAssetCategories")
            .WithSummary("Lists the asset categories included in the current study scope")
            .Produces<IReadOnlyList<AssetCategoryResponse>>(StatusCodes.Status200OK);

        group
            .MapGet("/schedule-statuses", () => TypedResults.Ok(ScheduleReferenceData.Statuses))
            .WithName("ListScheduleStatuses")
            .WithSummary("Lists persisted preventive maintenance schedule statuses")
            .Produces<IReadOnlyList<ScheduleReferenceResponse>>(StatusCodes.Status200OK);

        group
            .MapGet("/schedule-period-types", () => TypedResults.Ok(ScheduleReferenceData.PeriodTypes))
            .WithName("ListSchedulePeriodTypes")
            .WithSummary("Lists schedule period types accepted by the current API")
            .Produces<IReadOnlyList<ScheduleReferenceResponse>>(StatusCodes.Status200OK);

        group
            .MapGet("/schedule-quarters", () => TypedResults.Ok(ScheduleReferenceData.Quarters))
            .WithName("ListScheduleQuarters")
            .WithSummary("Lists controlled quarter metadata accepted by the current API")
            .Produces<IReadOnlyList<ScheduleReferenceResponse>>(StatusCodes.Status200OK);

        return api;
    }
}

public sealed record ScheduleReferenceResponse(string Code, string DisplayName);

internal static class ScheduleReferenceData
{
    internal static IReadOnlyList<ScheduleReferenceResponse> Statuses { get; } =
        ScheduleStatusCatalog.PersistedValues
            .Select(value => new ScheduleReferenceResponse(value, value))
            .ToArray();

    internal static IReadOnlyList<ScheduleReferenceResponse> PeriodTypes { get; } =
        SchedulePeriodTypeCatalog.PersistedValues
            .Select(value => new ScheduleReferenceResponse(value, value))
            .ToArray();

    internal static IReadOnlyList<ScheduleReferenceResponse> Quarters { get; } =
        ScheduleQuarterCatalog.PersistedValues
            .Select(value => new ScheduleReferenceResponse(value, value))
            .ToArray();
}
