using Microsoft.Extensions.Options;
using UniPM.Api.Features;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Features.MaintenanceReview;

public static class MaintenanceReviewEndpoints
{
    public static IEndpointRouteBuilder MapMaintenanceReviewEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/maintenance-review")
            .WithTags("Maintenance Review");

        group.MapPost("", HandleAsync)
        .WithName("CreateMaintenanceReview");

        return endpoints;
    }

    internal static async Task<IResult> HandleAsync(
            MaintenanceReviewRequest request,
            IOptions<MaintenanceReviewOptions> optionsAccessor,
            IMaintenanceReviewService service,
            CancellationToken cancellationToken)
    {
        var options = optionsAccessor.Value;
        if (!options.Enabled)
        {
            return ApiErrors.NotFound("Maintenance review is not enabled.");
        }

        var validationErrors = request.Validate(options.MaxFindingCharacters);
        if (validationErrors.Count > 0)
        {
            return ApiErrors.Validation(validationErrors);
        }

        try
        {
            var response = await service.ReviewAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (MaintenanceReviewAssetNotFoundException)
        {
            return ApiErrors.NotFound("Asset not found.");
        }
        catch (FusedMaintenanceAvailabilityException)
        {
            return ApiErrors.ServiceUnavailable("Maintenance retrieval is unavailable.");
        }
        catch (MaintenanceReviewAvailabilityException)
        {
            return ApiErrors.ServiceUnavailable("Maintenance review storage is unavailable.");
        }
        catch (MaintenanceReviewValidationException)
        {
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                ["findingText"] = ["The finding could not be used for maintenance retrieval."]
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MaintenanceReviewException)
        {
            return ApiErrors.InternalFailure("Maintenance review could not be completed.");
        }
        catch (FusedMaintenanceRetrievalException)
        {
            return ApiErrors.InternalFailure("Maintenance retrieval could not be completed.");
        }
        catch (Exception)
        {
            return ApiErrors.InternalFailure("Maintenance review could not be completed.");
        }
    }
}
