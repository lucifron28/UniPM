using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace UniPM.Api.Observability;

public sealed class UniPMMetrics
{
    public const string MeterName = "UniPM.Api";

    public const string RetrievalRequestsInstrumentName = "unipm.retrieval.requests";
    public const string RetrievalDurationInstrumentName = "unipm.retrieval.duration";
    public const string RetrievalResultsInstrumentName = "unipm.retrieval.results";
    public const string EmbeddingRebuildsInstrumentName = "unipm.embedding.rebuilds";
    public const string EmbeddingDurationInstrumentName = "unipm.embedding.duration";
    public const string EmbeddingDocumentsInstrumentName = "unipm.embedding.documents";
    public const string SearchProjectionRebuildsInstrumentName = "unipm.search_projection.rebuilds";
    public const string SearchProjectionDurationInstrumentName = "unipm.search_projection.duration";
    public const string SearchProjectionDocumentsInstrumentName = "unipm.search_projection.documents";

    private readonly Counter<long> retrievalRequests;
    private readonly Histogram<double> retrievalDuration;
    private readonly Histogram<long> retrievalResults;
    private readonly Counter<long> embeddingRebuilds;
    private readonly Histogram<double> embeddingDuration;
    private readonly Counter<long> embeddingDocuments;
    private readonly Counter<long> searchProjectionRebuilds;
    private readonly Histogram<double> searchProjectionDuration;
    private readonly Counter<long> searchProjectionDocuments;

    public UniPMMetrics(IMeterFactory meterFactory)
    {
        Meter = meterFactory.Create(new MeterOptions(MeterName)
        {
            Version = "1.0.0"
        });

        retrievalRequests = Meter.CreateCounter<long>(
            RetrievalRequestsInstrumentName,
            unit: "{request}");
        retrievalDuration = Meter.CreateHistogram<double>(
            RetrievalDurationInstrumentName,
            unit: "s");
        retrievalResults = Meter.CreateHistogram<long>(
            RetrievalResultsInstrumentName,
            unit: "{result}");
        embeddingRebuilds = Meter.CreateCounter<long>(
            EmbeddingRebuildsInstrumentName,
            unit: "{rebuild}");
        embeddingDuration = Meter.CreateHistogram<double>(
            EmbeddingDurationInstrumentName,
            unit: "s");
        embeddingDocuments = Meter.CreateCounter<long>(
            EmbeddingDocumentsInstrumentName,
            unit: "{document}");
        searchProjectionRebuilds = Meter.CreateCounter<long>(
            SearchProjectionRebuildsInstrumentName,
            unit: "{rebuild}");
        searchProjectionDuration = Meter.CreateHistogram<double>(
            SearchProjectionDurationInstrumentName,
            unit: "s");
        searchProjectionDocuments = Meter.CreateCounter<long>(
            SearchProjectionDocumentsInstrumentName,
            unit: "{document}");
    }

    internal Meter Meter { get; }

    internal void RecordRetrieval(
        string channel,
        string outcome,
        int resultCount,
        double durationSeconds)
    {
        retrievalRequests.Add(
            1,
            new TagList
            {
                { "channel", channel },
                { "outcome", outcome }
            });
        retrievalDuration.Record(
            durationSeconds,
            new TagList { { "channel", channel } });
        retrievalResults.Record(
            resultCount,
            new TagList { { "channel", channel } });
    }

    internal void RecordProjectionRebuild(
        string outcome,
        double durationSeconds,
        int total,
        int created,
        int updated,
        int removed)
    {
        searchProjectionRebuilds.Add(
            1,
            new TagList { { "outcome", outcome } });
        AddDocumentCount(searchProjectionDocuments, created, "created");
        AddDocumentCount(searchProjectionDocuments, updated, "updated");
        AddDocumentCount(searchProjectionDocuments, removed, "removed");
        searchProjectionDocuments.Add(total);
        searchProjectionDuration.Record(
            durationSeconds,
            new TagList { { "outcome", outcome } });
    }

    internal void RecordEmbeddingRebuild(
        string outcome,
        double durationSeconds,
        int total,
        int created,
        int updated,
        int skipped,
        int failed)
    {
        embeddingRebuilds.Add(
            1,
            new TagList { { "outcome", outcome } });
        AddDocumentCount(embeddingDocuments, created, "created");
        AddDocumentCount(embeddingDocuments, updated, "updated");
        AddDocumentCount(embeddingDocuments, skipped, "skipped");
        AddDocumentCount(embeddingDocuments, failed, "failed");
        embeddingDocuments.Add(total);
        embeddingDuration.Record(
            durationSeconds,
            new TagList { { "outcome", outcome } });
    }

    internal void RecordProjectionFailure(string outcome, double durationSeconds)
    {
        searchProjectionRebuilds.Add(
            1,
            new TagList { { "outcome", outcome } });
        searchProjectionDuration.Record(
            durationSeconds,
            new TagList { { "outcome", outcome } });
    }

    internal void RecordEmbeddingFailure(string outcome, double durationSeconds)
    {
        embeddingRebuilds.Add(
            1,
            new TagList { { "outcome", outcome } });
        embeddingDuration.Record(
            durationSeconds,
            new TagList { { "outcome", outcome } });
    }

    private static void AddDocumentCount(
        Counter<long> instrument,
        int count,
        string result)
    {
        if (count > 0)
        {
            instrument.Add(count, new TagList { { "result", result } });
        }
    }
}
