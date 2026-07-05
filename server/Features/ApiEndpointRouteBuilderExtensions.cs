using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features;

public static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapReferenceDataEndpoints();
        api.MapAssetsEndpoints();
        api.MapSchedulesEndpoints();
        api.MapInspectionsEndpoints();

        return endpoints;
    }
}
