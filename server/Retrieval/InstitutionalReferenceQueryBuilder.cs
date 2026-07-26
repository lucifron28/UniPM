using System.Text;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

internal static class InstitutionalReferenceQueryBuilder
{
    internal const int DefaultLimit = 10;
    internal const int MaxLimit = 100;
    internal const int MaxQueryLength = 256;
    internal const int MaxTokenCount = 8;
    internal const int MaxTokenLength = 64;

    public static InstitutionalReferenceQuery Build(InstitutionalReferenceSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedQuery = NormalizeWhitespace(request.Query);
        if (normalizedQuery.Length == 0)
        {
            throw new InstitutionalReferenceQueryValidationException("A non-blank institutional reference query is required.");
        }

        if (normalizedQuery.Length > MaxQueryLength)
        {
            throw new InstitutionalReferenceQueryValidationException(
                $"The institutional reference query cannot exceed {MaxQueryLength} characters.");
        }

        var tokens = LexicalMaintenanceQueryBuilder.TokenizeSearchableTerms(normalizedQuery);
        if (tokens.Count == 0 || tokens.Any(token => token.Length > MaxTokenLength))
        {
            throw new InstitutionalReferenceQueryValidationException("The institutional reference query contains no supported searchable terms.");
        }

        if (tokens.Count > MaxTokenCount)
        {
            throw new InstitutionalReferenceQueryValidationException(
                $"The institutional reference query cannot contain more than {MaxTokenCount} searchable terms.");
        }

        if (!AssetCategoryCatalog.TryNormalize(request.AssetCategory, out var assetCategory))
        {
            throw new InstitutionalReferenceQueryValidationException("A supported asset category is required.");
        }

        if (request.Limit is < 0)
        {
            throw new InstitutionalReferenceQueryValidationException("The institutional reference result limit cannot be negative.");
        }

        var limit = Math.Min(request.Limit.GetValueOrDefault(DefaultLimit), MaxLimit);
        return new InstitutionalReferenceQuery(
            normalizedQuery,
            string.Join(" AND ", tokens.Select(token => $"\"{token}*\"")),
            assetCategory,
            request.AsOfDate,
            limit == 0 ? DefaultLimit : limit);
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}
