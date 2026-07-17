using System.Diagnostics;
using UniPM.Api.Observability;

namespace UniPM.Api.Features.Retrieval;

internal sealed class MetricsLexicalMaintenanceRetriever(
    ILexicalMaintenanceRetriever inner,
    UniPMMetrics metrics)
    : ILexicalMaintenanceRetriever
{
    public async Task<IReadOnlyList<LexicalMaintenanceSearchResult>> SearchAsync(
        LexicalMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var results = await inner.SearchAsync(request, cancellationToken);
            metrics.RecordRetrieval(
                LexicalMaintenanceSearchResult.RetrievalChannelValue,
                results.Count > 0 ? "success" : "empty",
                results.Count,
                Stopwatch.GetElapsedTime(started).TotalSeconds);
            return results;
        }
        catch (LexicalMaintenanceQueryValidationException)
        {
            RecordFailure("validation_error", started);
            throw;
        }
        catch (LexicalMaintenanceAvailabilityException)
        {
            RecordFailure("unavailable", started);
            throw;
        }
        catch (OperationCanceledException)
        {
            RecordFailure("cancelled", started);
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
            LexicalMaintenanceSearchResult.RetrievalChannelValue,
            outcome,
            0,
            Stopwatch.GetElapsedTime(started).TotalSeconds);
    }
}

internal sealed class MetricsSemanticMaintenanceRetriever(
    ISemanticMaintenanceRetriever inner,
    UniPMMetrics metrics)
    : ISemanticMaintenanceRetriever
{
    public async Task<IReadOnlyList<SemanticMaintenanceSearchResult>> SearchAsync(
        SemanticMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var results = await inner.SearchAsync(request, cancellationToken);
            metrics.RecordRetrieval(
                SemanticMaintenanceSearchResult.RetrievalChannelValue,
                results.Count > 0 ? "success" : "empty",
                results.Count,
                Stopwatch.GetElapsedTime(started).TotalSeconds);
            return results;
        }
        catch (SemanticMaintenanceQueryValidationException)
        {
            RecordFailure("validation_error", started);
            throw;
        }
        catch (SemanticMaintenanceAvailabilityException)
        {
            RecordFailure("unavailable", started);
            throw;
        }
        catch (OperationCanceledException)
        {
            RecordFailure("cancelled", started);
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
            SemanticMaintenanceSearchResult.RetrievalChannelValue,
            outcome,
            0,
            Stopwatch.GetElapsedTime(started).TotalSeconds);
    }
}
