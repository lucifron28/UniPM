using System.Diagnostics;
using UniPM.Api.Observability;

namespace UniPM.Api.Features.Retrieval;

internal sealed class MetricsFusedMaintenanceRetriever(
    IFusedMaintenanceRetriever inner,
    UniPMMetrics metrics)
    : IFusedMaintenanceRetriever
{
    public async Task<FusedMaintenanceSearchResponse> SearchAsync(
        FusedMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await inner.SearchAsync(request, cancellationToken);
            metrics.RecordRetrieval(
                FusedMaintenanceSearchResult.RetrievalChannelValue,
                response.IsDegraded
                    ? "degraded"
                    : response.Results.Count == 0 ? "empty" : "success",
                response.Results.Count,
                Stopwatch.GetElapsedTime(started).TotalSeconds);
            return response;
        }
        catch (FusedMaintenanceQueryValidationException)
        {
            RecordFailure("validation_error", started);
            throw;
        }
        catch (FusedMaintenanceAvailabilityException)
        {
            RecordFailure("unavailable", started);
            throw;
        }
        catch (OperationCanceledException)
        {
            RecordFailure("cancelled", started);
            throw;
        }
        catch (FusedMaintenanceRetrievalException)
        {
            RecordFailure("failure", started);
            throw;
        }
        catch (Exception)
        {
            RecordFailure("failure", started);
            throw;
        }
    }

    private void RecordFailure(string outcome, long started)
    {
        metrics.RecordRetrieval(
            FusedMaintenanceSearchResult.RetrievalChannelValue,
            outcome,
            0,
            Stopwatch.GetElapsedTime(started).TotalSeconds);
    }
}
