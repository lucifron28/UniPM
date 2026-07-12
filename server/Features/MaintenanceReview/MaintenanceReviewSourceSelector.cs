using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Features.MaintenanceReview;

internal sealed record MaintenanceReviewSourceData(
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
    IReadOnlyList<string> IssueKeys,
    string? Remarks,
    string? ActionsRecommendations);

internal sealed record MaintenanceReviewCandidate(
    FusedMaintenanceSearchResult Retrieval,
    MaintenanceReviewSourceData Source);

internal sealed record MaintenanceReviewSelection(
    MaintenanceReviewCandidate Candidate,
    string ContextTier,
    IReadOnlyList<string> MatchedIssueKeys,
    IReadOnlyList<string> MatchedReasons,
    int MatchingContextFieldCount,
    int OverlappingIssueKeyCount);

internal sealed class MaintenanceReviewSourceSelector
{
    public IReadOnlyList<MaintenanceReviewSelection> Select(
        Guid targetAssetId,
        string targetAssetCategory,
        string? targetBuilding,
        string? targetDepartment,
        string? targetLocation,
        IReadOnlyList<string> findingIssueKeys,
        IReadOnlyList<MaintenanceReviewCandidate> candidates,
        int maxSourceRecords)
    {
        ArgumentNullException.ThrowIfNull(findingIssueKeys);
        ArgumentNullException.ThrowIfNull(candidates);
        if (maxSourceRecords is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSourceRecords));
        }

        if (!AssetCategoryCatalog.TryNormalize(targetAssetCategory, out var normalizedCategory))
        {
            throw new ArgumentException("A supported target asset category is required.", nameof(targetAssetCategory));
        }

        var normalizedIssueKeys = findingIssueKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var deduplicated = candidates
            .GroupBy(candidate => candidate.Retrieval.InspectionId)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Retrieval.FusionScore)
                .ThenByDescending(candidate => candidate.Retrieval.DateInspected)
                .ThenBy(candidate => candidate.Retrieval.InspectionId)
                .First())
            .ToArray();

        var selections = new List<MaintenanceReviewSelection>();
        foreach (var candidate in deduplicated)
        {
            var source = candidate.Source;
            if (!string.Equals(source.AssetCategory, normalizedCategory, StringComparison.Ordinal))
            {
                continue;
            }

            var matchedIssueKeys = source.IssueKeys
                .Where(normalizedIssueKeys.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            var sameAsset = source.AssetId == targetAssetId;
            var matchingContextFields = CountMatchingContextFields(
                targetBuilding,
                targetDepartment,
                targetLocation,
                source);

            if (!sameAsset && (normalizedIssueKeys.Length == 0 || matchedIssueKeys.Length == 0))
            {
                continue;
            }

            var tier = sameAsset
                ? matchedIssueKeys.Length > 0
                    ? MaintenanceReviewContextTier.SameAssetIssueMatch
                    : MaintenanceReviewContextTier.SameAssetHistory
                : matchingContextFields > 0
                    ? MaintenanceReviewContextTier.ContextualIssueMatch
                    : MaintenanceReviewContextTier.SameCategoryIssueMatch;

            selections.Add(new MaintenanceReviewSelection(
                candidate,
                tier,
                matchedIssueKeys,
                BuildMatchedReasons(
                    sameAsset,
                    string.Equals(source.AssetCategory, normalizedCategory, StringComparison.Ordinal),
                    matchedIssueKeys,
                    targetBuilding,
                    targetDepartment,
                    targetLocation,
                    source,
                    candidate.Retrieval),
                matchingContextFields,
                matchedIssueKeys.Length));
        }

        return selections
            .OrderBy(selection => TierOrder(selection.ContextTier))
            .ThenByDescending(selection => selection.OverlappingIssueKeyCount)
            .ThenByDescending(selection => selection.MatchingContextFieldCount)
            .ThenByDescending(selection => selection.Candidate.Retrieval.FusionScore)
            .ThenByDescending(selection => selection.Candidate.Retrieval.DateInspected)
            .ThenBy(selection => selection.Candidate.Retrieval.InspectionId)
            .Take(maxSourceRecords)
            .ToArray();
    }

    private static int TierOrder(string tier)
        => tier switch
        {
            MaintenanceReviewContextTier.SameAssetIssueMatch => 0,
            MaintenanceReviewContextTier.SameAssetHistory => 1,
            MaintenanceReviewContextTier.ContextualIssueMatch => 2,
            MaintenanceReviewContextTier.SameCategoryIssueMatch => 3,
            _ => int.MaxValue
        };

    private static int CountMatchingContextFields(
        string? targetBuilding,
        string? targetDepartment,
        string? targetLocation,
        MaintenanceReviewSourceData source)
    {
        var count = 0;
        if (Matches(targetLocation, source.Location)) count++;
        if (Matches(targetBuilding, source.Building)) count++;
        if (Matches(targetDepartment, source.Department)) count++;
        return count;
    }

    private static IReadOnlyList<string> BuildMatchedReasons(
        bool sameAsset,
        bool sameCategory,
        IReadOnlyList<string> matchedIssueKeys,
        string? targetBuilding,
        string? targetDepartment,
        string? targetLocation,
        MaintenanceReviewSourceData source,
        FusedMaintenanceSearchResult retrieval)
    {
        var reasons = new List<string>();
        if (sameAsset) reasons.Add("same_asset");
        if (matchedIssueKeys.Count > 0) reasons.Add("same_issue_key");
        if (Matches(targetLocation, source.Location)) reasons.Add("same_location");
        if (Matches(targetBuilding, source.Building)) reasons.Add("same_building");
        if (Matches(targetDepartment, source.Department)) reasons.Add("same_department");
        if (sameCategory) reasons.Add("same_category");
        if (retrieval.LexicalRank is not null) reasons.Add("lexical_match");
        if (retrieval.SemanticRank is not null) reasons.Add("semantic_match");
        return reasons;
    }

    private static bool Matches(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

