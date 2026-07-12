namespace UniPM.Api.Features.MaintenanceReview;

public sealed class MaintenanceReviewOptions
{
    public const string SectionName = "MaintenanceReview";

    public bool Enabled { get; set; }
    public int MaxSourceRecords { get; set; } = 5;
    public int RetrievalCandidateLimit { get; set; } = 20;
    public int MaxFindingCharacters { get; set; } = 2000;
}

public static class MaintenanceReviewEvidenceStatus
{
    public const string SameAssetHistoryFound = "same_asset_history_found";
    public const string SimilarAssetFallback = "similar_asset_fallback";
    public const string SameCategoryFallback = "same_category_fallback";
    public const string InsufficientEvidence = "insufficient_evidence";
}

public static class MaintenanceReviewSummaryStatus
{
    public const string Generated = "generated";
    public const string NotRequested = "not_requested";
    public const string Disabled = "disabled";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string Failed = "failed";
    public const string NotGeneratedInsufficientEvidence = "not_generated_insufficient_evidence";
}

public static class MaintenanceReviewContextTier
{
    public const string SameAssetIssueMatch = "same_asset_issue_match";
    public const string SameAssetHistory = "same_asset_history";
    public const string ContextualIssueMatch = "contextual_issue_match";
    public const string SameCategoryIssueMatch = "same_category_issue_match";
}

