using System.Text;
using System.Text.RegularExpressions;
using UniPM.Api.Features.Retrieval;

namespace UniPM.Api.Features.MaintenanceReview;

internal static class MaintenanceReviewRetrievalQueryBuilder
{
    public const int MaxQueryLength = 256;
    public const int MaxTermCount = LexicalMaintenanceQueryBuilder.MaxTokenCount;

    private static readonly Regex SanitizerPlaceholderPattern = new(
        @"\[(?:EMPLOYEE_ID|EMAIL|PHONE)_\d+\]",
        RegexOptions.CultureInvariant);

    public static MaintenanceReviewRetrievalQuery Build(
        string findingText,
        IReadOnlyList<string> issueKeys)
    {
        ArgumentNullException.ThrowIfNull(issueKeys);

        var normalizedFinding = MaintenanceReviewText.Normalize(findingText);
        var normalizedIssueKeys = NormalizeIssueKeys(issueKeys);
        var terms = new List<string>(MaxTermCount);
        var seenTerms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issueKey in normalizedIssueKeys)
        {
            AddTerms(issueKey.Replace('_', ' '), terms, seenTerms);
        }

        var findingWithoutPlaceholders = SanitizerPlaceholderPattern.Replace(normalizedFinding, " ");
        AddTerms(findingWithoutPlaceholders, terms, seenTerms);
        var query = BuildBoundedQuery(terms);
        if (query.Length == 0)
        {
            throw new ArgumentException("A non-blank retrieval query is required.", nameof(findingText));
        }

        return new MaintenanceReviewRetrievalQuery(query, normalizedIssueKeys);
    }

    public static MaintenanceReviewRetrievalQuery? BuildCanonicalIssueQuery(
        IReadOnlyList<string> issueKeys)
    {
        ArgumentNullException.ThrowIfNull(issueKeys);

        var normalizedIssueKeys = NormalizeIssueKeys(issueKeys);
        if (normalizedIssueKeys.Length == 0)
        {
            return null;
        }

        var terms = new List<string>(MaxTermCount);
        var seenTerms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issueKey in normalizedIssueKeys)
        {
            AddTerms(issueKey.Replace('_', ' '), terms, seenTerms);
        }

        return new MaintenanceReviewRetrievalQuery(
            BuildBoundedQuery(terms),
            normalizedIssueKeys);
    }

    private static string[] NormalizeIssueKeys(IReadOnlyList<string> issueKeys)
        => issueKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    private static void AddTerms(string value, ICollection<string> terms, ISet<string> seenTerms)
    {
        foreach (var term in LexicalMaintenanceQueryBuilder.TokenizeSearchableTerms(value))
        {
            if (terms.Count == MaxTermCount)
            {
                return;
            }

            if (seenTerms.Add(term))
            {
                terms.Add(term);
            }
        }
    }

    private static string BuildBoundedQuery(IEnumerable<string> terms)
    {
        var builder = new StringBuilder(MaxQueryLength);
        foreach (var term in terms)
        {
            var remainingLength = MaxQueryLength - builder.Length - (builder.Length == 0 ? 0 : 1);
            if (remainingLength <= 0)
            {
                break;
            }

            var boundedTerm = TruncateAtScalarBoundary(term, remainingLength);
            if (boundedTerm.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(boundedTerm);
        }

        return builder.ToString();
    }

    private static string TruncateAtScalarBoundary(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var safeLength = maxLength;
        if (safeLength > 0 && char.IsHighSurrogate(value[safeLength - 1]))
        {
            safeLength--;
        }

        return value[..safeLength];
    }
}

internal sealed record MaintenanceReviewRetrievalQuery(
    string Text,
    IReadOnlyList<string> IssueKeys);
