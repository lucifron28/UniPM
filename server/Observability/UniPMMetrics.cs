using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace UniPM.Api.Observability;

public sealed class UniPMMetrics
{
    public const string MeterName = "UniPM.Api";

    public const string RetrievalRequestsInstrumentName = "unipm.retrieval.requests";
    public const string RetrievalDurationInstrumentName = "unipm.retrieval.duration";
    public const string RetrievalResultsInstrumentName = "unipm.retrieval.results";

    private readonly Counter<long> retrievalRequests;
    private readonly Histogram<double> retrievalDuration;
    private readonly Histogram<long> retrievalResults;

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

}
