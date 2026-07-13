using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.MaintenanceReview;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features;

public static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1");

        api.MapReferenceDataEndpoints();
        api.MapAuthEndpoints();
        api.MapAssetsEndpoints();
        api.MapSchedulesEndpoints();
        api.MapInspectionsEndpoints();
        api.MapMaintenanceReviewEndpoints();

        return endpoints;
    }
}
