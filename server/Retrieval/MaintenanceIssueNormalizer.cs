using System.Text;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

public sealed class MaintenanceIssueNormalizer
{
    private static readonly string[] NegationPrefixes = ["no", "without", "walang", "hindi"];
    private readonly IReadOnlyList<NormalizedIssueDefinition> issues;

    public MaintenanceIssueNormalizer(MaintenanceIssueLexiconLoader loader)
    {
        var document = loader.Load();
        issues = document.Issues
            .Select(issue => new NormalizedIssueDefinition(
                issue.Key,
                issue.AssetCategory.Trim().ToLowerInvariant(),
                issue.Aliases
                    .Select(alias => new NormalizedAlias(alias, MaintenanceIssueText.Normalize(alias)))
                    .ToList()))
            .ToList();
    }

    public IReadOnlyList<MaintenanceIssueMatch> Normalize(string text, string assetCategory)
    {
        if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var normalizedCategory))
        {
            throw new ArgumentException(
                "A supported asset category is required for maintenance issue normalization.",
                nameof(assetCategory));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalizedText = MaintenanceIssueText.Normalize(text);
        return issues
            .Where(issue => string.Equals(issue.AssetCategory, normalizedCategory, StringComparison.Ordinal))
            .Select(issue =>
            {
                var matchedAliases = issue.Aliases
                    .Where(alias => ContainsUnnegatedAlias(normalizedText, alias.NormalizedValue))
                    .OrderByDescending(alias => alias.NormalizedValue.Length)
                    .ThenBy(alias => alias.Value, StringComparer.Ordinal)
                    .ToList();

                return matchedAliases.Count == 0
                    ? null
                    : new MaintenanceIssueMatch(
                        issue.Key,
                        matchedAliases.Max(alias => alias.NormalizedValue.Length),
                        matchedAliases.Select(alias => alias.Value).ToList());
            })
            .Where(match => match is not null)
            .Select(match => match!)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.IssueKey, StringComparer.Ordinal)
            .ToList();
    }

    private static bool ContainsUnnegatedAlias(string normalizedText, string normalizedAlias)
    {
        var paddedText = $" {normalizedText} ";
        var paddedAlias = $" {normalizedAlias} ";
        var searchStart = 0;

        while (true)
        {
            var aliasIndex = paddedText.IndexOf(paddedAlias, searchStart, StringComparison.Ordinal);
            if (aliasIndex < 0)
            {
                return false;
            }

            var aliasStart = aliasIndex + 1;
            if (!IsNegated(paddedText, aliasStart, normalizedAlias))
            {
                return true;
            }

            searchStart = aliasIndex + paddedAlias.Length;
        }
    }

    private static bool IsNegated(string paddedText, int aliasStart, string normalizedAlias)
    {
        if (normalizedAlias.StartsWith("hindi ", StringComparison.Ordinal))
        {
            return false;
        }

        var beforeAlias = paddedText[..aliasStart].TrimEnd();
        if (NegationPrefixes.Any(prefix => beforeAlias.EndsWith($" {prefix}", StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    private sealed record NormalizedIssueDefinition(
        string Key,
        string AssetCategory,
        IReadOnlyList<NormalizedAlias> Aliases);

    private sealed record NormalizedAlias(string Value, string NormalizedValue);
}

internal static class MaintenanceIssueText
{
    public static string Normalize(string value)
    {
        var compatibilityNormalized = value.Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(compatibilityNormalized.Length);
        var needsSpace = false;

        foreach (var character in compatibilityNormalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (needsSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(character);
                needsSpace = false;
            }
            else
            {
                needsSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
