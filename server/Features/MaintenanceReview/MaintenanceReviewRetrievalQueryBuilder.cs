namespace UniPM.Api.Features.MaintenanceReview;

internal static class MaintenanceReviewRetrievalQueryBuilder
{
    public const int MaxQueryLength = 256;
    public const int MaxTermCount = 8;

    public static MaintenanceReviewRetrievalQuery Build(
        string findingText,
        IReadOnlyList<string> issueKeys)
    {
        ArgumentNullException.ThrowIfNull(issueKeys);

        var normalizedFinding = MaintenanceReviewText.Normalize(findingText);
        var normalizedIssueKeys = issueKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var boundedFinding = TakeWholeWords(normalizedFinding, MaxQueryLength);
        var boundedTerms = string.Join(
            ' ',
            boundedFinding.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(MaxTermCount));
        var query = boundedTerms;
        if (query.Length == 0)
        {
            throw new ArgumentException("A non-blank retrieval query is required.", nameof(findingText));
        }

        return new MaintenanceReviewRetrievalQuery(query, normalizedIssueKeys);
    }

    private static string TakeWholeWords(string value, int maxLength)
    {
        if (maxLength <= 0 || value.Length == 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        var boundary = value.LastIndexOf(' ', maxLength - 1);
        if (boundary > 0)
        {
            return value[..boundary];
        }

        var safeLength = maxLength;
        if (safeLength < value.Length && safeLength > 0 && char.IsHighSurrogate(value[safeLength - 1]))
        {
            safeLength--;
        }

        return value[..safeLength];
    }
}

internal sealed record MaintenanceReviewRetrievalQuery(
    string Text,
    IReadOnlyList<string> IssueKeys);
