using System.Text.Json.Serialization;

namespace UniPM.Api.Features.MaintenanceReview;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class MaintenanceReviewRequest
{
    public Guid AssetId { get; set; }
    public string FindingText { get; set; } = string.Empty;
    public bool GenerateSummary { get; set; } = true;

    internal Dictionary<string, string[]> Validate(int maxFindingCharacters)
    {
        var errors = new Dictionary<string, string[]>();
        if (AssetId == Guid.Empty)
        {
            errors[nameof(AssetId)] = ["Asset ID is required."];
        }

        var normalizedFinding = MaintenanceReviewText.Normalize(FindingText);
        if (normalizedFinding.Length == 0)
        {
            errors[nameof(FindingText)] = ["Finding text is required."];
        }
        else if (normalizedFinding.Length > maxFindingCharacters)
        {
            errors[nameof(FindingText)] =
                [$"Finding text must not exceed {maxFindingCharacters} characters."];
        }

        return errors;
    }

    internal string NormalizeFinding() => MaintenanceReviewText.Normalize(FindingText);
}

public sealed record MaintenanceReviewResponse(
    MaintenanceReviewAssetResponse TargetAsset,
    IReadOnlyList<string> FindingIssueKeys,
    string EvidenceStatus,
    bool RecurringSameAssetPatternSupported,
    MaintenanceReviewRetrievalStatusResponse RetrievalStatus,
    string SummaryStatus,
    string? Summary,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<MaintenanceReviewSourceRecordResponse> SourceRecords);

public sealed record MaintenanceReviewAssetResponse(
    Guid Id,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location);

public sealed record MaintenanceReviewRetrievalStatusResponse(
    bool IsDegraded,
    int PassesExecuted,
    string LexicalStatus,
    string SemanticStatus,
    string FusionMethod,
    int ReciprocalRankConstant);

public sealed record MaintenanceReviewSourceRecordResponse(
    string SourceLabel,
    Guid SourceRecordId,
    Guid AssetId,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    DateTimeOffset InspectionDate,
    bool IsOperational,
    IReadOnlyList<string> IssueKeys,
    IReadOnlyList<string> MatchedIssueKeys,
    IReadOnlyList<string> MatchedReasons,
    string ContextTier,
    string? Remarks,
    string? ActionsRecommendations,
    MaintenanceReviewRetrievalTraceResponse RetrievalTrace);

public sealed record MaintenanceReviewRetrievalTraceResponse(
    double FusionScore,
    int? LexicalRank,
    int? SemanticRank,
    int MatchedChannelCount);

internal static class MaintenanceReviewText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var needsSpace = false;
        foreach (var character in value.Normalize(System.Text.NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(character))
            {
                needsSpace = builder.Length > 0;
                continue;
            }

            if (needsSpace)
            {
                builder.Append(' ');
                needsSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}

