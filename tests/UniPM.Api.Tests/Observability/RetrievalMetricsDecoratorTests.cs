using System.Diagnostics;
using System.Diagnostics.Metrics;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Observability;

namespace UniPM.Api.Tests.Observability;

public sealed class RetrievalMetricsDecoratorTests
{
    [Fact]
    public async Task Lexical_success_preserves_order_and_records_one_bounded_measurement_set()
    {
        var expected = new[] { CreateLexicalResult(1), CreateLexicalResult(2) };
        var inner = new FakeLexicalRetriever(_ => Task.FromResult<IReadOnlyList<LexicalMaintenanceSearchResult>>(expected));
        using var recorder = new MetricRecorder();
        var decorator = new MetricsLexicalMaintenanceRetriever(inner, recorder.Metrics);

        var actual = await decorator.SearchAsync(new LexicalMaintenanceSearchRequest("private query text"));

        Assert.Equal(expected, actual);
        Assert.Equal(1, inner.CallCount);
        Assert.Equal(1, recorder.Count(UniPMMetrics.RetrievalRequestsInstrumentName));
        Assert.Equal(1, recorder.Count(UniPMMetrics.RetrievalDurationInstrumentName));
        Assert.Equal(1, recorder.Count(UniPMMetrics.RetrievalResultsInstrumentName));
        Assert.Equal(2, recorder.Single(UniPMMetrics.RetrievalResultsInstrumentName).Value);
        Assert.Equal("lexical", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["channel"]);
        Assert.Equal("success", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
        Assert.All(recorder.All, measurement =>
            Assert.DoesNotContain("private query text", measurement.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lexical_empty_records_empty_outcome()
    {
        using var recorder = new MetricRecorder();
        var decorator = new MetricsLexicalMaintenanceRetriever(
            new FakeLexicalRetriever(_ => Task.FromResult<IReadOnlyList<LexicalMaintenanceSearchResult>>([])),
            recorder.Metrics);

        var results = await decorator.SearchAsync(new LexicalMaintenanceSearchRequest("filter"));

        Assert.Empty(results);
        Assert.Equal("empty", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
        Assert.Equal(0, recorder.Single(UniPMMetrics.RetrievalResultsInstrumentName).Value);
    }

    [Theory]
    [InlineData("validation_error")]
    [InlineData("unavailable")]
    [InlineData("failure")]
    public async Task Lexical_failures_record_the_expected_outcome_and_rethrow_the_original_exception(
        string outcome)
    {
        Exception exception = outcome switch
        {
            "validation_error" => new LexicalMaintenanceQueryValidationException("secret query"),
            "unavailable" => new LexicalMaintenanceAvailabilityException("provider unavailable"),
            _ => new InvalidOperationException("unexpected failure")
        };
        using var recorder = new MetricRecorder();
        var decorator = new MetricsLexicalMaintenanceRetriever(
            new FakeLexicalRetriever(_ => Task.FromException<IReadOnlyList<LexicalMaintenanceSearchResult>>(exception)),
            recorder.Metrics);

        var actual = await Assert.ThrowsAnyAsync<Exception>(
            () => decorator.SearchAsync(new LexicalMaintenanceSearchRequest("secret query")));

        Assert.Same(exception, actual);
        Assert.Equal(outcome, recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
    }

    [Fact]
    public async Task Lexical_cancellation_records_cancelled_and_rethrows_the_original_exception()
    {
        var exception = new OperationCanceledException("cancelled");
        using var recorder = new MetricRecorder();
        var decorator = new MetricsLexicalMaintenanceRetriever(
            new FakeLexicalRetriever(_ => Task.FromException<IReadOnlyList<LexicalMaintenanceSearchResult>>(exception)),
            recorder.Metrics);

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.SearchAsync(new LexicalMaintenanceSearchRequest("cancelled query")));

        Assert.Same(exception, actual);
        Assert.Equal("cancelled", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
    }

    [Fact]
    public async Task Semantic_success_records_channel_and_result_count()
    {
        using var recorder = new MetricRecorder();
        var decorator = new MetricsSemanticMaintenanceRetriever(
            new FakeSemanticRetriever(_ => Task.FromResult<IReadOnlyList<SemanticMaintenanceSearchResult>>(
                [CreateSemanticResult(1)])),
            recorder.Metrics);

        var results = await decorator.SearchAsync(new SemanticMaintenanceSearchRequest("semantic query"));

        Assert.Single(results);
        Assert.Equal("semantic", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["channel"]);
        Assert.Equal("success", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
        Assert.Equal(1, recorder.Single(UniPMMetrics.RetrievalResultsInstrumentName).Value);
    }

    [Theory]
    [InlineData("validation_error")]
    [InlineData("unavailable")]
    [InlineData("failure")]
    public async Task Semantic_failures_map_to_bounded_outcomes(string outcome)
    {
        Exception exception = outcome switch
        {
            "validation_error" => new SemanticMaintenanceQueryValidationException("secret query"),
            "unavailable" => new SemanticMaintenanceAvailabilityException("provider unavailable"),
            _ => new SemanticMaintenanceDataException("invalid vector data")
        };
        using var recorder = new MetricRecorder();
        var decorator = new MetricsSemanticMaintenanceRetriever(
            new FakeSemanticRetriever(_ => Task.FromException<IReadOnlyList<SemanticMaintenanceSearchResult>>(exception)),
            recorder.Metrics);

        var actual = await Assert.ThrowsAnyAsync<Exception>(
            () => decorator.SearchAsync(new SemanticMaintenanceSearchRequest("secret query")));

        Assert.Same(exception, actual);
        Assert.Equal(outcome, recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
    }

    [Fact]
    public async Task Semantic_cancellation_records_cancelled()
    {
        var exception = new OperationCanceledException("cancelled");
        using var recorder = new MetricRecorder();
        var decorator = new MetricsSemanticMaintenanceRetriever(
            new FakeSemanticRetriever(_ => Task.FromException<IReadOnlyList<SemanticMaintenanceSearchResult>>(exception)),
            recorder.Metrics);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => decorator.SearchAsync(new SemanticMaintenanceSearchRequest("cancelled query")));

        Assert.Equal("cancelled", recorder.Single(UniPMMetrics.RetrievalRequestsInstrumentName).Tags["outcome"]);
    }

    private static LexicalMaintenanceSearchResult CreateLexicalResult(int suffix)
        => new(
            Guid.Parse($"00000000-0000-0000-0000-00000000000{suffix}"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            true,
            suffix);

    private static SemanticMaintenanceSearchResult CreateSemanticResult(int suffix)
        => new(
            Guid.Parse($"00000000-0000-0000-0000-00000000000{suffix}"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            true,
            0.9d);

    private sealed class FakeLexicalRetriever(
        Func<LexicalMaintenanceSearchRequest, Task<IReadOnlyList<LexicalMaintenanceSearchResult>>> operation)
        : ILexicalMaintenanceRetriever
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<LexicalMaintenanceSearchResult>> SearchAsync(
            LexicalMaintenanceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return operation(request);
        }
    }

    private sealed class FakeSemanticRetriever(
        Func<SemanticMaintenanceSearchRequest, Task<IReadOnlyList<SemanticMaintenanceSearchResult>>> operation)
        : ISemanticMaintenanceRetriever
    {
        public Task<IReadOnlyList<SemanticMaintenanceSearchResult>> SearchAsync(
            SemanticMaintenanceSearchRequest request,
            CancellationToken cancellationToken = default)
            => operation(request);
    }

    private sealed class MetricRecorder : IDisposable
    {
        private readonly MeterListener listener = new();
        private readonly List<MetricMeasurement> measurements = [];

        public MetricRecorder()
        {
            Metrics = new UniPMMetrics(new TestMeterFactory());
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == UniPMMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>(
                (instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.SetMeasurementEventCallback<double>(
                (instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.Start();
        }

        public UniPMMetrics Metrics { get; }
        public IReadOnlyList<MetricMeasurement> All => measurements;

        public int Count(string instrumentName)
            => measurements.Count(measurement => measurement.InstrumentName == instrumentName);

        public MetricMeasurement Single(string instrumentName)
            => Assert.Single(measurements, measurement => measurement.InstrumentName == instrumentName);

        public void Dispose() => listener.Dispose();

        private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copiedTags = tags.ToArray()
                .ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);
            measurements.Add(new MetricMeasurement(instrument.Name, Convert.ToDouble(value), copiedTags));
        }
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options)
            => new(options.Name, options.Version);

        public void Dispose()
        {
        }
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        double Value,
        IReadOnlyDictionary<string, string> Tags)
    {
        public override string ToString()
            => $"{InstrumentName}={Value};{string.Join(',', Tags.Select(tag => $"{tag.Key}={tag.Value}"))}";
    }
}
