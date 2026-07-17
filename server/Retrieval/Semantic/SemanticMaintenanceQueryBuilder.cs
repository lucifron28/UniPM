using System.Text;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

internal static class SemanticMaintenanceQueryBuilder
{
    internal const int DefaultLimit = 20;
    internal const int MaxLimit = 100;
    internal const int MaxQueryLength = 256;
    internal const int MaxMetadataFilterLength = 256;
    internal const int MaxCandidateCount = 500;

    public static SemanticMaintenanceQuery Build(
        SemanticMaintenanceSearchRequest request,
        MaintenanceIssueNormalizer issueNormalizer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(issueNormalizer);

        var normalizedQuery = NormalizeWhitespace(request.Query);
        if (normalizedQuery.Length == 0)
        {
            throw new SemanticMaintenanceQueryValidationException("A non-blank semantic query is required.");
        }

        if (normalizedQuery.Length > MaxQueryLength)
        {
            throw new SemanticMaintenanceQueryValidationException(
                $"The semantic query cannot exceed {MaxQueryLength} characters.");
        }

        if (request.Limit is < 0)
        {
            throw new SemanticMaintenanceQueryValidationException("The semantic result limit cannot be negative.");
        }

        var limit = Math.Min(request.Limit.GetValueOrDefault(DefaultLimit), MaxLimit);
        if (limit == 0)
        {
            limit = DefaultLimit;
        }

        string? assetCategory = null;
        var issueKeys = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(request.AssetCategory))
        {
            if (!AssetCategoryCatalog.TryNormalize(request.AssetCategory, out assetCategory))
            {
                throw new SemanticMaintenanceQueryValidationException(
                    $"Asset category '{request.AssetCategory}' is not supported.");
            }

            issueKeys = request.IssueKeys is not null
                ? request.IssueKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray()
                : issueNormalizer
                    .Normalize(normalizedQuery, assetCategory)
                    .Select(match => match.IssueKey)
                    .ToArray();
        }

        var building = NormalizeMetadataFilter(request.Building, nameof(request.Building));
        var department = NormalizeMetadataFilter(request.Department, nameof(request.Department));
        var location = NormalizeMetadataFilter(request.Location, nameof(request.Location));

        if (request.DateFrom is not null
            && request.DateTo is not null
            && request.DateFrom > request.DateTo)
        {
            throw new SemanticMaintenanceQueryValidationException("DateFrom cannot be later than DateTo.");
        }

        return new SemanticMaintenanceQuery(
            normalizedQuery,
            MaintenanceEmbeddingInput.BuildQueryInput(normalizedQuery, issueKeys),
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

    private static string? NormalizeMetadataFilter(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = NormalizeWhitespace(value);
        if (normalized.Length > MaxMetadataFilterLength)
        {
            throw new SemanticMaintenanceQueryValidationException(
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

internal sealed record SemanticMaintenanceQuery(
    string NormalizedQuery,
    string EmbeddingInput,
    int Limit,
    Guid? AssetId,
    string? AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    bool? IsOperational,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo);
