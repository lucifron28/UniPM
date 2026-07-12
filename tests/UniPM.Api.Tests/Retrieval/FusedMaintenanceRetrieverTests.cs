using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Tests.Retrieval;

public sealed class FusedMaintenanceRetrieverTests
{
    [Fact]
    public async Task Success_executes_each_channel_once_with_identical_bounded_filters()
    {
        var lexical = new FakeLexical(_ => [Lexical(1), Lexical(2)]);
        var semantic = new FakeSemantic(_ => [Semantic(2), Semantic(1)]);
        var retriever = new FusedMaintenanceRetriever(lexical, semantic);
        var request = new FusedMaintenanceSearchRequest(
            "pressure finding",
            Limit: 5,
            AssetCategory: " FIRE-EXTINGUISHER ",
            Building: " Main Building ",
            Department: "GSD",
            Location: " Room 1 ",
            IsOperational: false,
            DateFrom: AtManila(2025, 1, 1),
            DateTo: AtManila(2025, 12, 31));

        var response = await retriever.SearchAsync(request);

        Assert.False(response.IsDegraded);
        Assert.Equal(FusedRetrievalChannelStatus.Success, response.Lexical.Status);
        Assert.Equal(FusedRetrievalChannelStatus.Success, response.Semantic.Status);
        Assert.Equal(1, lexical.CallCount);
        Assert.Equal(1, semantic.CallCount);
        Assert.Equal(20, lexical.Requests[0].Limit);
        Assert.Equal(lexical.Requests[0].AssetCategory, semantic.Requests[0].AssetCategory);
        Assert.Equal(lexical.Requests[0].Building, semantic.Requests[0].Building);
        Assert.Equal(lexical.Requests[0].Department, semantic.Requests[0].Department);
        Assert.Equal(lexical.Requests[0].Location, semantic.Requests[0].Location);
        Assert.Equal(lexical.Requests[0].DateFrom, semantic.Requests[0].DateFrom);
        Assert.Equal(2, response.Results[0].MatchedChannelCount);
        Assert.Equal(60, response.ReciprocalRankConstant);
        Assert.Equal("rrf", response.FusionMethod);
    }

    [Fact]
    public async Task Lexical_empty_and_semantic_success_is_valid_non_degraded_semantic_only_fusion()
    {
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => []),
            new FakeSemantic(_ => [Semantic(1)]));

        var response = await retriever.SearchAsync(new FusedMaintenanceSearchRequest("paraphrase"));

        Assert.False(response.IsDegraded);
        Assert.Equal(FusedRetrievalChannelStatus.Empty, response.Lexical.Status);
        Assert.Equal(FusedRetrievalChannelStatus.Success, response.Semantic.Status);
        Assert.Single(response.Results);
        Assert.Null(response.Results[0].LexicalRank);
        Assert.Equal(1, response.Results[0].SemanticRank);
    }

    [Fact]
    public async Task Semantic_empty_and_both_empty_are_not_degraded()
    {
        var lexicalOnly = new FusedMaintenanceRetriever(
            new FakeLexical(_ => [Lexical(1)]),
            new FakeSemantic(_ => []));
        var bothEmpty = new FusedMaintenanceRetriever(
            new FakeLexical(_ => []),
            new FakeSemantic(_ => []));

        var lexicalResponse = await lexicalOnly.SearchAsync(new FusedMaintenanceSearchRequest("finding"));
        var emptyResponse = await bothEmpty.SearchAsync(new FusedMaintenanceSearchRequest("finding"));

        Assert.False(lexicalResponse.IsDegraded);
        Assert.Equal(FusedRetrievalChannelStatus.Empty, lexicalResponse.Semantic.Status);
        Assert.Single(lexicalResponse.Results);
        Assert.False(emptyResponse.IsDegraded);
        Assert.Empty(emptyResponse.Results);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("execution")]
    [InlineData("data")]
    public async Task Semantic_operational_failures_return_lexical_results_as_explicit_degradation(string failure)
    {
        Exception exception = failure switch
        {
            "unavailable" => new SemanticMaintenanceAvailabilityException("provider secret should not escape"),
            "execution" => new SemanticMaintenanceExecutionException("provider payload should not escape"),
            _ => new SemanticMaintenanceDataException("vector should not escape")
        };
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => [Lexical(1), Lexical(2)]),
            new FakeSemantic(_ => throw exception));

        var response = await retriever.SearchAsync(new FusedMaintenanceSearchRequest("pressure"));

        Assert.True(response.IsDegraded);
        Assert.Equal(
            failure == "unavailable"
                ? FusedRetrievalChannelStatus.Unavailable
                : FusedRetrievalChannelStatus.Failed,
            response.Semantic.Status);
        Assert.Equal([Id(1), Id(2)], response.Results.Select(item => item.InspectionId));
        Assert.DoesNotContain("provider secret", response.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("provider payload", response.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("vector should", response.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Semantic_validation_failure_is_a_fused_validation_error()
    {
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => [Lexical(1)]),
            new FakeSemantic(_ => throw new SemanticMaintenanceQueryValidationException("bad semantic request")));

        await Assert.ThrowsAsync<FusedMaintenanceQueryValidationException>(
            () => retriever.SearchAsync(new FusedMaintenanceSearchRequest("pressure")));
    }

    [Fact]
    public async Task Unexpected_semantic_failure_is_not_converted_to_degraded_success()
    {
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => [Lexical(1)]),
            new FakeSemantic(_ => throw new InvalidOperationException("unexpected internal failure")));

        var exception = await Assert.ThrowsAsync<FusedMaintenanceExecutionException>(
            () => retriever.SearchAsync(new FusedMaintenanceSearchRequest("pressure")));

        Assert.Equal(
            "Semantic retrieval failed unexpectedly during fused retrieval.",
            exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Theory]
    [InlineData("validation")]
    [InlineData("availability")]
    [InlineData("execution")]
    public async Task Lexical_failures_are_fused_failures_and_do_not_start_semantic(string failure)
    {
        Exception exception = failure switch
        {
            "validation" => new LexicalMaintenanceQueryValidationException("invalid"),
            "availability" => new LexicalMaintenanceAvailabilityException("unavailable"),
            _ => new LexicalMaintenanceExecutionException("failed")
        };
        var semantic = new FakeSemantic(_ => [Semantic(1)]);
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => throw exception),
            semantic);

        await Assert.ThrowsAnyAsync<FusedMaintenanceRetrievalException>(
            () => retriever.SearchAsync(new FusedMaintenanceSearchRequest("pressure")));
        Assert.Equal(0, semantic.CallCount);
    }

    [Fact]
    public async Task Cancellation_from_either_channel_is_propagated()
    {
        var lexicalCancellation = new OperationCanceledException("cancelled");
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FusedMaintenanceRetriever(
                new FakeLexical(_ => throw lexicalCancellation),
                new FakeSemantic(_ => [Semantic(1)])).SearchAsync(new FusedMaintenanceSearchRequest("pressure")));

        var semanticCancellation = new OperationCanceledException("cancelled");
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FusedMaintenanceRetriever(
                new FakeLexical(_ => [Lexical(1)]),
                new FakeSemantic(_ => throw semanticCancellation)).SearchAsync(new FusedMaintenanceSearchRequest("pressure")));
    }

    [Fact]
    public async Task Invalid_fused_request_is_rejected_before_channel_execution()
    {
        var lexical = new FakeLexical(_ => [Lexical(1)]);
        var semantic = new FakeSemantic(_ => [Semantic(1)]);
        var retriever = new FusedMaintenanceRetriever(lexical, semantic);

        await Assert.ThrowsAsync<FusedMaintenanceQueryValidationException>(
            () => retriever.SearchAsync(new FusedMaintenanceSearchRequest(
                "pressure",
                DateFrom: AtManila(2025, 2, 1),
                DateTo: AtManila(2025, 1, 1))));
        Assert.Equal(0, lexical.CallCount);
        Assert.Equal(0, semantic.CallCount);
    }

    [Fact]
    public async Task Metadata_conflict_is_not_silently_resolved()
    {
        var semantic = Semantic(1) with { Department = "Other" };
        var retriever = new FusedMaintenanceRetriever(
            new FakeLexical(_ => [Lexical(1)]),
            new FakeSemantic(_ => [semantic]));

        await Assert.ThrowsAsync<FusedMaintenanceDataIntegrityException>(
            () => retriever.SearchAsync(new FusedMaintenanceSearchRequest("pressure")));
    }

    private static LexicalMaintenanceSearchResult Lexical(int suffix)
        => new(
            Id(suffix),
            AssetId(suffix),
            ScheduleId(suffix),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            "Main Building",
            "GSD",
            $"Room {suffix}",
            AtManila(2025, 1, suffix),
            false,
            suffix);

    private static SemanticMaintenanceSearchResult Semantic(int suffix)
        => new(
            Id(suffix),
            AssetId(suffix),
            ScheduleId(suffix),
            $"FE-{suffix:000}",
            "fire-extinguisher",
            "Main Building",
            "GSD",
            $"Room {suffix}",
            AtManila(2025, 1, suffix),
            false,
            1d / suffix);

    private static Guid Id(int suffix)
        => Guid.Parse($"00000000-0000-0000-0000-0000000000{suffix:00}");

    private static Guid AssetId(int suffix)
        => Guid.Parse($"10000000-0000-0000-0000-0000000000{suffix:00}");

    private static Guid ScheduleId(int suffix)
        => Guid.Parse($"20000000-0000-0000-0000-0000000000{suffix:00}");

    private static DateTimeOffset AtManila(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, TimeSpan.FromHours(8));

    private sealed class FakeLexical(
        Func<LexicalMaintenanceSearchRequest, IReadOnlyList<LexicalMaintenanceSearchResult>> operation)
        : ILexicalMaintenanceRetriever
    {
        public int CallCount { get; private set; }
        public List<LexicalMaintenanceSearchRequest> Requests { get; } = [];

        public Task<IReadOnlyList<LexicalMaintenanceSearchResult>> SearchAsync(
            LexicalMaintenanceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            return Task.FromResult(operation(request));
        }
    }

    private sealed class FakeSemantic(
        Func<SemanticMaintenanceSearchRequest, IReadOnlyList<SemanticMaintenanceSearchResult>> operation)
        : ISemanticMaintenanceRetriever
    {
        public int CallCount { get; private set; }
        public List<SemanticMaintenanceSearchRequest> Requests { get; } = [];

        public Task<IReadOnlyList<SemanticMaintenanceSearchResult>> SearchAsync(
            SemanticMaintenanceSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Requests.Add(request);
            return Task.FromResult(operation(request));
        }
    }
}
