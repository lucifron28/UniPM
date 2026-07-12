namespace UniPM.Api.Features.Retrieval;

internal sealed class FusedMaintenanceRetriever(
    ILexicalMaintenanceRetriever lexicalRetriever,
    ISemanticMaintenanceRetriever semanticRetriever)
    : IFusedMaintenanceRetriever
{
    public async Task<FusedMaintenanceSearchResponse> SearchAsync(
        FusedMaintenanceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = FusedMaintenanceQueryBuilder.Build(request);
        var lexicalRequest = new LexicalMaintenanceSearchRequest(
            query.NormalizedQuery,
            query.CandidateLimit,
            query.AssetId,
            query.AssetCategory,
            query.Building,
            query.Department,
            query.Location,
            query.IsOperational,
            query.DateFrom,
            query.DateTo);

        IReadOnlyList<LexicalMaintenanceSearchResult> lexicalResults;
        try
        {
            lexicalResults = await lexicalRetriever.SearchAsync(lexicalRequest, cancellationToken);
        }
        catch (LexicalMaintenanceQueryValidationException)
        {
            throw new FusedMaintenanceQueryValidationException(
                "The fused request was rejected by lexical retrieval.");
        }
        catch (LexicalMaintenanceAvailabilityException exception)
        {
            throw new FusedMaintenanceAvailabilityException(
                "Lexical retrieval is unavailable for the fused request.",
                exception);
        }
        catch (LexicalMaintenanceExecutionException exception)
        {
            throw new FusedMaintenanceExecutionException(
                "Lexical retrieval failed for the fused request.",
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new FusedMaintenanceExecutionException(
                "Lexical retrieval failed for the fused request.",
                exception);
        }

        var lexicalStatus = new FusedRetrievalChannelExecution(
            LexicalMaintenanceSearchResult.RetrievalChannelValue,
            lexicalResults.Count == 0
                ? FusedRetrievalChannelStatus.Empty
                : FusedRetrievalChannelStatus.Success,
            lexicalResults.Count);
        var semanticRequest = new SemanticMaintenanceSearchRequest(
            query.NormalizedQuery,
            query.CandidateLimit,
            query.AssetId,
            query.AssetCategory,
            query.Building,
            query.Department,
            query.Location,
            query.IsOperational,
            query.DateFrom,
            query.DateTo);

        IReadOnlyList<SemanticMaintenanceSearchResult> semanticResults;
        try
        {
            semanticResults = await semanticRetriever.SearchAsync(semanticRequest, cancellationToken);
        }
        catch (SemanticMaintenanceQueryValidationException)
        {
            throw new FusedMaintenanceQueryValidationException(
                "The fused request was rejected by semantic retrieval.");
        }
        catch (SemanticMaintenanceAvailabilityException)
        {
            return BuildDegradedResponse(
                lexicalResults,
                lexicalStatus,
                new FusedRetrievalChannelExecution(
                    SemanticMaintenanceSearchResult.RetrievalChannelValue,
                    FusedRetrievalChannelStatus.Unavailable,
                    0),
                query);
        }
        catch (SemanticMaintenanceExecutionException)
        {
            return BuildDegradedResponse(
                lexicalResults,
                lexicalStatus,
                new FusedRetrievalChannelExecution(
                    SemanticMaintenanceSearchResult.RetrievalChannelValue,
                    FusedRetrievalChannelStatus.Failed,
                    0),
                query);
        }
        catch (SemanticMaintenanceDataException)
        {
            return BuildDegradedResponse(
                lexicalResults,
                lexicalStatus,
                new FusedRetrievalChannelExecution(
                    SemanticMaintenanceSearchResult.RetrievalChannelValue,
                    FusedRetrievalChannelStatus.Failed,
                    0),
                query);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new FusedMaintenanceExecutionException(
                "Semantic retrieval failed unexpectedly during fused retrieval.",
                exception);
        }

        var semanticStatus = new FusedRetrievalChannelExecution(
            SemanticMaintenanceSearchResult.RetrievalChannelValue,
            semanticResults.Count == 0
                ? FusedRetrievalChannelStatus.Empty
                : FusedRetrievalChannelStatus.Success,
            semanticResults.Count);

        return BuildResponse(lexicalResults, semanticResults, lexicalStatus, semanticStatus, false, query);
    }

    private static FusedMaintenanceSearchResponse BuildDegradedResponse(
        IReadOnlyList<LexicalMaintenanceSearchResult> lexicalResults,
        FusedRetrievalChannelExecution lexicalStatus,
        FusedRetrievalChannelExecution semanticStatus,
        FusedMaintenanceQuery query)
        => BuildResponse(lexicalResults, [], lexicalStatus, semanticStatus, true, query);

    private static FusedMaintenanceSearchResponse BuildResponse(
        IReadOnlyList<LexicalMaintenanceSearchResult> lexicalResults,
        IReadOnlyList<SemanticMaintenanceSearchResult> semanticResults,
        FusedRetrievalChannelExecution lexicalStatus,
        FusedRetrievalChannelExecution semanticStatus,
        bool isDegraded,
        FusedMaintenanceQuery query)
    {
        IReadOnlyList<FusedMaintenanceSearchResult> results;
        try
        {
            results = ReciprocalRankFusion.Fuse(lexicalResults, semanticResults, query.Limit);
        }
        catch (FusedMaintenanceDataIntegrityException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FusedMaintenanceExecutionException(
                "Fused retrieval could not combine channel results.",
                exception);
        }

        return new FusedMaintenanceSearchResponse(
            results,
            lexicalStatus,
            semanticStatus,
            isDegraded,
            ReciprocalRankFusion.Method,
            ReciprocalRankFusion.ReciprocalRankConstant,
            query.CandidateLimit);
    }
}
