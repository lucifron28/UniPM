using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features;

public static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapReferenceDataEndpoints();

        return endpoints;
    }
}
