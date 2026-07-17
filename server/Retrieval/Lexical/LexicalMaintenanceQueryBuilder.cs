using System.Text;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

internal static class LexicalMaintenanceQueryBuilder
{
    internal const int DefaultLimit = 20;
    internal const int MaxLimit = 100;
    internal const int MaxQueryLength = 256;
    internal const int MaxTokenCount = 8;
    internal const int MaxMetadataFilterLength = 256;

    private static readonly HashSet<string> FullTextOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND",
        "OR",
        "NOT",
        "NEAR",
        "FORMSOF",
        "ISABOUT",
        "WEIGHT",
        "MAX",
        "MIN",
        "CUSTOM",
        "GENERIC",
        "SIMPLE"
    };

    public static LexicalMaintenanceQuery Build(LexicalMaintenanceSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedQuery = NormalizeWhitespace(request.Query);
        if (normalizedQuery.Length > MaxQueryLength)
        {
            throw new LexicalMaintenanceQueryValidationException(
                $"The lexical query cannot exceed {MaxQueryLength} characters.");
        }

        var tokens = TokenizeSearchableTerms(normalizedQuery);
        if (tokens.Count == 0)
        {
            throw new LexicalMaintenanceQueryValidationException(
                "A non-blank lexical query is required after punctuation and operator filtering.");
        }

        if (tokens.Count > MaxTokenCount)
        {
            throw new LexicalMaintenanceQueryValidationException(
                $"The lexical query cannot contain more than {MaxTokenCount} searchable terms.");
        }

        if (request.Limit is < 0)
        {
            throw new LexicalMaintenanceQueryValidationException("The lexical result limit cannot be negative.");
        }

        var limit = Math.Min(request.Limit.GetValueOrDefault(DefaultLimit), MaxLimit);
        if (limit == 0)
        {
            limit = DefaultLimit;
        }

        string? assetCategory = null;
        if (!string.IsNullOrWhiteSpace(request.AssetCategory))
        {
            if (!AssetCategoryCatalog.TryNormalize(request.AssetCategory, out assetCategory))
            {
                throw new LexicalMaintenanceQueryValidationException(
                    $"Asset category '{request.AssetCategory}' is not supported.");
            }
        }

        var building = NormalizeMetadataFilter(request.Building, nameof(request.Building));
        var department = NormalizeMetadataFilter(request.Department, nameof(request.Department));
        var location = NormalizeMetadataFilter(request.Location, nameof(request.Location));

        if (request.DateFrom is not null
            && request.DateTo is not null
            && request.DateFrom > request.DateTo)
        {
            throw new LexicalMaintenanceQueryValidationException(
                "DateFrom cannot be later than DateTo.");
        }

        return new LexicalMaintenanceQuery(
            normalizedQuery,
            string.Join(" AND ", tokens.Select(token => $"\"{token}*\"")),
            limit,
            request.AssetId,
            assetCategory,
            building,
            department,
            location,
            request.IsOperational,
            request.DateFrom,
            request.DateTo);
    }

    internal static IReadOnlyList<string> TokenizeSearchableTerms(string value)
    {
        var compatibilityNormalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var tokens = new List<string>();
        var token = new StringBuilder();

        void CompleteToken()
        {
            if (token.Length == 0)
            {
                return;
            }

            var value = token.ToString();
            token.Clear();

            if (!FullTextOperators.Contains(value))
            {
                tokens.Add(value);
            }
        }

        foreach (var character in compatibilityNormalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(character);
            }
            else
            {
                CompleteToken();
            }
        }

        CompleteToken();
        return tokens;
    }

    private static string? NormalizeMetadataFilter(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = NormalizeWhitespace(value);
        if (normalized.Length > MaxMetadataFilterLength)
        {
            throw new LexicalMaintenanceQueryValidationException(
                $"The {fieldName} filter cannot exceed {MaxMetadataFilterLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var needsSpace = false;

        foreach (var character in value.Normalize(NormalizationForm.FormKC))
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

internal sealed record LexicalMaintenanceQuery(
    string NormalizedQuery,
    string SearchCondition,
    int Limit,
    Guid? AssetId,
    string? AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    bool? IsOperational,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo);
