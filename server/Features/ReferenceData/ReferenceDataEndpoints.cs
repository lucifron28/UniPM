namespace UniPM.Api.Features.ReferenceData;

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

        return api;
    }
}
