using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniPM.Api.Data;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Models;

namespace UniPM.Api.Features.MaintenanceReview;

internal interface IMaintenanceReviewService
{
    Task<MaintenanceReviewResponse> ReviewAsync(
        MaintenanceReviewRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class MaintenanceReviewService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IFusedMaintenanceRetriever fusedRetriever,
    MaintenanceIssueNormalizer issueNormalizer,
    MaintenanceReviewSourceSelector sourceSelector,
    PrivacySanitizerService sanitizer,
    MaintenanceReviewPromptBuilder promptBuilder,
    ISummaryService summaryService,
    IOptions<MaintenanceReviewOptions> reviewOptionsAccessor,
    IOptions<SummaryOptions> summaryOptionsAccessor)
    : IMaintenanceReviewService
{
    private readonly MaintenanceReviewOptions reviewOptions = reviewOptionsAccessor.Value;
    private readonly SummaryOptions summaryOptions = summaryOptionsAccessor.Value;

    public async Task<MaintenanceReviewResponse> ReviewAsync(
        MaintenanceReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var findingText = request.NormalizeFinding();
        var sanitizationSession = sanitizer.CreateSession();
        Asset asset;
        try
        {
            await using var assetContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            asset = await assetContext.Assets
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == request.AssetId, cancellationToken)
                ?? throw new MaintenanceReviewAssetNotFoundException();
        }
        catch (MaintenanceReviewAssetNotFoundException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new MaintenanceReviewAvailabilityException(
                "Maintenance review storage is unavailable.",
                exception);
        }

        var findingIssueKeys = issueNormalizer
            .Normalize(findingText, asset.AssetCategory)
            .Select(match => match.IssueKey)
            .ToArray();
        var retrievalQuery = MaintenanceReviewRetrievalQueryBuilder.Build(
            sanitizationSession.Sanitize(findingText),
            findingIssueKeys);

        var passResponses = new List<FusedMaintenanceSearchResponse>();
        passResponses.Add(await ExecuteFusedPassAsync(
            retrievalQuery,
            asset,
            includeAssetFilter: true,
            cancellationToken));

        if (passResponses[0].Results.Count < reviewOptions.MaxSourceRecords)
        {
            passResponses.Add(await ExecuteFusedPassAsync(
                retrievalQuery,
                asset,
                includeAssetFilter: false,
                cancellationToken));
        }

        // SQL FTS combines ordinary query terms with AND. When a multilingual
        // finding yields no candidates, retry only the already-normalized issue terms.
        var canonicalIssueQuery = MaintenanceReviewRetrievalQueryBuilder
            .BuildCanonicalIssueQuery(findingIssueKeys);
        if (canonicalIssueQuery is not null && passResponses.All(response => response.Results.Count == 0))
        {
            passResponses.Add(await ExecuteFusedPassAsync(
                canonicalIssueQuery,
                asset,
                includeAssetFilter: true,
                cancellationToken));

            if (passResponses[^1].Results.Count == 0)
            {
                passResponses.Add(await ExecuteFusedPassAsync(
                    canonicalIssueQuery,
                    asset,
                    includeAssetFilter: false,
                    cancellationToken));
            }
        }

        var fusedCandidates = passResponses
            .SelectMany(response => response.Results)
            .GroupBy(result => result.InspectionId)
            .Select(group => group
                .OrderByDescending(result => result.FusionScore)
                .ThenByDescending(result => result.DateInspected)
                .ThenBy(result => result.InspectionId)
                .First())
            .ToArray();

        var sourceData = await LoadSourceDataAsync(
            fusedCandidates.Select(candidate => candidate.InspectionId).ToArray(),
            cancellationToken);
        var sourceLookup = sourceData.ToDictionary(source => source.InspectionId);
        var candidates = new List<MaintenanceReviewCandidate>(fusedCandidates.Length);
        foreach (var fusedCandidate in fusedCandidates)
        {
            if (!sourceLookup.TryGetValue(fusedCandidate.InspectionId, out var source))
            {
                throw new MaintenanceReviewDataIntegrityException(
                    "A fused retrieval result did not map to a persisted inspection source.");
            }

            VerifySourceMetadata(fusedCandidate, source);
            candidates.Add(new MaintenanceReviewCandidate(fusedCandidate, source));
        }

        var selections = sourceSelector.Select(
            asset.Id,
            asset.AssetCategory,
            asset.Building,
            asset.Department,
            asset.Location,
            findingIssueKeys,
            candidates,
            reviewOptions.MaxSourceRecords);
        var evidenceStatus = DeriveEvidenceStatus(selections);
        var recurring = SupportsRecurringPattern(selections, asset.Id, findingIssueKeys);
        var limitations = BuildRetrievalLimitations(passResponses, evidenceStatus);
        var sourceResponses = selections
            .Select((selection, index) => ToSourceResponse(selection, $"SRC-{index + 1}"))
            .ToArray();
        var retrievalStatus = BuildRetrievalStatus(passResponses);

        if (evidenceStatus == MaintenanceReviewEvidenceStatus.InsufficientEvidence)
        {
            limitations.Add("No bounded source record met the same-asset or issue-matched category evidence rules.");
            return new MaintenanceReviewResponse(
                ToAssetResponse(asset),
                findingIssueKeys,
                evidenceStatus,
                recurring,
                retrievalStatus,
                MaintenanceReviewSummaryStatus.NotGeneratedInsufficientEvidence,
                null,
                limitations,
                sourceResponses);
        }

        if (!request.GenerateSummary)
        {
            return new MaintenanceReviewResponse(
                ToAssetResponse(asset),
                findingIssueKeys,
                evidenceStatus,
                recurring,
                retrievalStatus,
                MaintenanceReviewSummaryStatus.NotRequested,
                null,
                limitations,
                sourceResponses);
        }

        if (!summaryOptions.Enabled)
        {
            limitations.Add("Summary generation is disabled; the selected source records remain available for human verification.");
            return new MaintenanceReviewResponse(
                ToAssetResponse(asset),
                findingIssueKeys,
                evidenceStatus,
                recurring,
                retrievalStatus,
                MaintenanceReviewSummaryStatus.Disabled,
                null,
                limitations,
                sourceResponses);
        }

        var summaryStatus = MaintenanceReviewSummaryStatus.Failed;
        string? summary = null;
        try
        {
            var prompt = promptBuilder.Build(
                BuildPromptInput(
                    findingText,
                    asset,
                    evidenceStatus,
                    recurring,
                    selections,
                    sourceResponses,
                    sanitizationSession),
                summaryOptions);
            var generated = await summaryService.GenerateAsync(
                new SummaryGenerationRequest(
                    prompt.SystemMessage,
                    prompt.UserMessage,
                    prompt.IncludedSourceLabels,
                    prompt.TemplateVersion),
                cancellationToken);
            summary = SummaryOutputValidator.Validate(
                generated.Content,
                prompt.IncludedSourceLabels,
                summaryOptions.MaxOutputCharacters);
            summaryStatus = MaintenanceReviewSummaryStatus.Generated;
        }
        catch (SummaryServiceAvailabilityException)
        {
            summaryStatus = MaintenanceReviewSummaryStatus.ProviderUnavailable;
            limitations.Add("The configured summary provider was unavailable; source records were returned without a generated summary.");
        }
        catch (SummaryServiceExecutionException)
        {
            summaryStatus = MaintenanceReviewSummaryStatus.ProviderUnavailable;
            limitations.Add("The summary provider could not complete the request; source records were returned without a generated summary.");
        }
        catch (SummaryServiceDataException)
        {
            summaryStatus = MaintenanceReviewSummaryStatus.Failed;
            limitations.Add("The generated summary did not meet the bounded source-citation contract; source records were returned without it.");
        }
        catch (MaintenanceReviewPromptException)
        {
            summaryStatus = MaintenanceReviewSummaryStatus.Failed;
            limitations.Add("The selected evidence could not fit the bounded summary prompt; source records were returned without it.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            summaryStatus = MaintenanceReviewSummaryStatus.Failed;
            limitations.Add("Summary generation failed safely; source records were returned without a generated summary.");
        }

        return new MaintenanceReviewResponse(
            ToAssetResponse(asset),
            findingIssueKeys,
            evidenceStatus,
            recurring,
            retrievalStatus,
            summaryStatus,
            summary,
            limitations,
            sourceResponses);
    }

    private async Task<FusedMaintenanceSearchResponse> ExecuteFusedPassAsync(
        MaintenanceReviewRetrievalQuery retrievalQuery,
        Asset asset,
        bool includeAssetFilter,
        CancellationToken cancellationToken)
    {
        try
        {
            return await fusedRetriever.SearchAsync(
                new FusedMaintenanceSearchRequest(
                    retrievalQuery.Text,
                    reviewOptions.RetrievalCandidateLimit,
                    includeAssetFilter ? asset.Id : null,
                    asset.AssetCategory,
                    IssueKeys: retrievalQuery.IssueKeys),
                cancellationToken);
        }
        catch (FusedMaintenanceQueryValidationException exception)
        {
            throw new MaintenanceReviewValidationException(
                "The maintenance review request could not be used for retrieval.",
                exception);
        }
    }

    private async Task<IReadOnlyList<MaintenanceReviewSourceData>> LoadSourceDataAsync(
        IReadOnlyList<Guid> inspectionIds,
        CancellationToken cancellationToken)
    {
        if (inspectionIds.Count == 0)
        {
            return [];
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from inspection in context.InspectionRecords.AsNoTracking()
                join document in context.MaintenanceSearchDocuments.AsNoTracking()
                    on inspection.Id equals document.InspectionId
                where inspectionIds.Contains(inspection.Id)
                select new MaintenanceReviewSourceProjection(
                    inspection.Id,
                    inspection.AssetId,
                    inspection.ScheduleId,
                    document.AssetCode,
                    document.AssetCategory,
                    document.Building,
                    document.Department,
                    document.Location,
                    inspection.DateInspected,
                    inspection.IsOperational,
                    document.IssueKeysJson,
                    inspection.Remarks,
                    inspection.ActionsRecommendations))
                .ToListAsync(cancellationToken);

            return rows.Select(ToSourceData).ToArray();
        }
        catch (MaintenanceReviewDataIntegrityException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new MaintenanceReviewDataIntegrityException(
                "A maintenance source document contains invalid issue-key data.",
                exception);
        }
        catch (Exception exception)
        {
            throw new MaintenanceReviewAvailabilityException(
                "Maintenance review source storage is unavailable.",
                exception);
        }
    }

    private static MaintenanceReviewSourceData ToSourceData(MaintenanceReviewSourceProjection row)
    {
        try
        {
            var issueKeys = JsonSerializer.Deserialize<string[]>(row.IssueKeysJson) ?? [];
            return new MaintenanceReviewSourceData(
                row.InspectionId,
                row.AssetId,
                row.ScheduleId,
                row.AssetCode,
                row.AssetCategory,
                row.Building,
                row.Department,
                row.Location,
                row.DateInspected,
                row.IsOperational,
                issueKeys.Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                row.Remarks,
                row.ActionsRecommendations);
        }
        catch (JsonException exception)
        {
            throw new MaintenanceReviewDataIntegrityException(
                "A maintenance source document contains invalid issue-key data.",
                exception);
        }
    }

    private static void VerifySourceMetadata(
        FusedMaintenanceSearchResult retrieval,
        MaintenanceReviewSourceData source)
    {
        if (retrieval.AssetId != source.AssetId
            || retrieval.ScheduleId != source.ScheduleId
            || !string.Equals(retrieval.AssetCode, source.AssetCode, StringComparison.Ordinal)
            || !string.Equals(retrieval.AssetCategory, source.AssetCategory, StringComparison.Ordinal)
            || !string.Equals(retrieval.Building, source.Building, StringComparison.Ordinal)
            || !string.Equals(retrieval.Department, source.Department, StringComparison.Ordinal)
            || !string.Equals(retrieval.Location, source.Location, StringComparison.Ordinal)
            || retrieval.DateInspected != source.DateInspected
            || retrieval.IsOperational != source.IsOperational)
        {
            throw new MaintenanceReviewDataIntegrityException(
                "Retrieval metadata did not match the persisted maintenance source.");
        }
    }

    private MaintenanceReviewPromptInput BuildPromptInput(
        string findingText,
        Asset asset,
        string evidenceStatus,
        bool recurring,
        IReadOnlyList<MaintenanceReviewSelection> selections,
        IReadOnlyList<MaintenanceReviewSourceRecordResponse> sourceResponses,
        PrivacySanitizationSession session)
    {
        var sourceById = sourceResponses.ToDictionary(source => source.SourceRecordId);
        return new MaintenanceReviewPromptInput(
            session.Sanitize(findingText),
            new MaintenanceReviewPromptAsset(
                session.Sanitize(asset.AssetCode),
                session.Sanitize(asset.AssetCategory),
                session.Sanitize(asset.Building),
                session.Sanitize(asset.Department),
                session.Sanitize(asset.Location)),
            evidenceStatus,
            recurring,
            selections.Select(selection =>
            {
                var source = selection.Candidate.Source;
                var response = sourceById[source.InspectionId];
                return new MaintenanceReviewPromptSource(
                    response.SourceLabel,
                    selection.ContextTier,
                    response.MatchedReasons,
                    session.Sanitize(source.AssetCode),
                    session.Sanitize(source.AssetCategory),
                    session.Sanitize(source.Building),
                    session.Sanitize(source.Department),
                    session.Sanitize(source.Location),
                    source.DateInspected,
                    source.IsOperational,
                    source.IssueKeys,
                    session.Sanitize(source.Remarks),
                    session.Sanitize(source.ActionsRecommendations));
            }).ToArray());
    }

    private static MaintenanceReviewSourceRecordResponse ToSourceResponse(
        MaintenanceReviewSelection selection,
        string sourceLabel)
    {
        var source = selection.Candidate.Source;
        var retrieval = selection.Candidate.Retrieval;
        return new MaintenanceReviewSourceRecordResponse(
            sourceLabel,
            source.InspectionId,
            source.AssetId,
            source.AssetCode,
            source.AssetCategory,
            source.Building,
            source.Department,
            source.Location,
            source.DateInspected,
            source.IsOperational,
            source.IssueKeys,
            selection.MatchedIssueKeys,
            selection.MatchedReasons,
            selection.ContextTier,
            source.Remarks,
            source.ActionsRecommendations,
            new MaintenanceReviewRetrievalTraceResponse(
                retrieval.FusionScore,
                retrieval.LexicalRank,
                retrieval.SemanticRank,
                retrieval.MatchedChannelCount));
    }

    private static string DeriveEvidenceStatus(IReadOnlyList<MaintenanceReviewSelection> selections)
    {
        if (selections.Any(selection => selection.Candidate.Source.AssetId == selection.Candidate.Retrieval.AssetId
            && selection.ContextTier is MaintenanceReviewContextTier.SameAssetIssueMatch or MaintenanceReviewContextTier.SameAssetHistory))
        {
            return MaintenanceReviewEvidenceStatus.SameAssetHistoryFound;
        }

        if (selections.Any(selection => selection.ContextTier == MaintenanceReviewContextTier.ContextualIssueMatch))
        {
            return MaintenanceReviewEvidenceStatus.SimilarAssetFallback;
        }

        if (selections.Any(selection => selection.ContextTier == MaintenanceReviewContextTier.SameCategoryIssueMatch))
        {
            return MaintenanceReviewEvidenceStatus.SameCategoryFallback;
        }

        return MaintenanceReviewEvidenceStatus.InsufficientEvidence;
    }

    private static bool SupportsRecurringPattern(
        IReadOnlyList<MaintenanceReviewSelection> selections,
        Guid targetAssetId,
        IReadOnlyList<string> findingIssueKeys)
        => findingIssueKeys.Any(issueKey => selections
            .Where(selection => selection.Candidate.Source.AssetId == targetAssetId)
            .Count(selection => selection.Candidate.Source.IssueKeys.Contains(issueKey, StringComparer.Ordinal)) >= 2);

    private static List<string> BuildRetrievalLimitations(
        IReadOnlyList<FusedMaintenanceSearchResponse> responses,
        string evidenceStatus)
    {
        var limitations = new List<string>();
        if (responses.Any(response => response.IsDegraded))
        {
            limitations.Add("Semantic retrieval was unavailable or failed for at least one pass; lexical fallback was used and fused results are degraded.");
        }

        if (evidenceStatus is MaintenanceReviewEvidenceStatus.SimilarAssetFallback
            or MaintenanceReviewEvidenceStatus.SameCategoryFallback)
        {
            limitations.Add("The selected evidence is fallback context from other assets and must not be treated as same-asset history.");
        }

        return limitations;
    }

    private static MaintenanceReviewRetrievalStatusResponse BuildRetrievalStatus(
        IReadOnlyList<FusedMaintenanceSearchResponse> responses)
    {
        var semanticStatus = responses
            .Select(response => response.Semantic.Status)
            .OrderByDescending(StatusStrength)
            .FirstOrDefault(FusedRetrievalChannelStatus.Empty);
        var latest = responses[0];
        var lexicalStatus = responses.Any(response =>
                response.Lexical.Status == FusedRetrievalChannelStatus.Success)
            ? FusedRetrievalChannelStatus.Success
            : responses.Any(response =>
                response.Lexical.Status == FusedRetrievalChannelStatus.Failed)
                ? FusedRetrievalChannelStatus.Failed
                : responses.Any(response =>
                    response.Lexical.Status == FusedRetrievalChannelStatus.Unavailable)
                    ? FusedRetrievalChannelStatus.Unavailable
                    : FusedRetrievalChannelStatus.Empty;
        return new MaintenanceReviewRetrievalStatusResponse(
            responses.Any(response => response.IsDegraded),
            responses.Count,
            lexicalStatus.ToString().ToLowerInvariant(),
            semanticStatus.ToString().ToLowerInvariant(),
            latest.FusionMethod,
            latest.ReciprocalRankConstant);
    }

    private static int StatusStrength(FusedRetrievalChannelStatus status)
        => status switch
        {
            FusedRetrievalChannelStatus.Failed => 4,
            FusedRetrievalChannelStatus.Unavailable => 3,
            FusedRetrievalChannelStatus.Empty => 2,
            _ => 1
        };

    private static MaintenanceReviewAssetResponse ToAssetResponse(Asset asset)
        => new(asset.Id, asset.AssetCode, asset.AssetCategory, asset.Building, asset.Department, asset.Location);

    private sealed record MaintenanceReviewSourceProjection(
        Guid InspectionId,
        Guid AssetId,
        Guid ScheduleId,
        string AssetCode,
        string AssetCategory,
        string? Building,
        string? Department,
        string? Location,
        DateTimeOffset DateInspected,
        bool IsOperational,
        string IssueKeysJson,
        string? Remarks,
        string? ActionsRecommendations);
}

internal sealed class MaintenanceReviewAssetNotFoundException()
    : InvalidOperationException("The requested asset was not found.");

internal enum MaintenanceReviewFailureKind
{
    Validation,
    Availability,
    Execution,
    Data
}

internal class MaintenanceReviewException(
    MaintenanceReviewFailureKind kind,
    string message,
    Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public MaintenanceReviewFailureKind Kind { get; } = kind;
}

internal sealed class MaintenanceReviewValidationException(string message, Exception? innerException = null)
    : MaintenanceReviewException(MaintenanceReviewFailureKind.Validation, message, innerException);

internal sealed class MaintenanceReviewAvailabilityException(string message, Exception? innerException = null)
    : MaintenanceReviewException(MaintenanceReviewFailureKind.Availability, message, innerException);

internal sealed class MaintenanceReviewExecutionException(string message, Exception? innerException = null)
    : MaintenanceReviewException(MaintenanceReviewFailureKind.Execution, message, innerException);

internal sealed class MaintenanceReviewDataIntegrityException(string message, Exception? innerException = null)
    : MaintenanceReviewException(MaintenanceReviewFailureKind.Data, message, innerException);
