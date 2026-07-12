using System.Text;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

internal static class FusedMaintenanceQueryBuilder
{
    internal const int DefaultLimit = 10;
    internal const int DefaultCandidateDepth = 20;
    internal const int MaxLimit = 100;
    internal const int MaxQueryLength = 256;
    internal const int MaxMetadataFilterLength = 256;

    public static FusedMaintenanceQuery Build(FusedMaintenanceSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedQuery = NormalizeWhitespace(request.Query);
        if (normalizedQuery.Length == 0)
        {
            throw new FusedMaintenanceQueryValidationException("A non-blank fused query is required.");
        }

        if (normalizedQuery.Length > MaxQueryLength)
        {
            throw new FusedMaintenanceQueryValidationException(
                $"The fused query cannot exceed {MaxQueryLength} characters.");
        }

        if (request.Limit is < 0)
        {
            throw new FusedMaintenanceQueryValidationException("The fused result limit cannot be negative.");
        }

        var limit = Math.Min(request.Limit.GetValueOrDefault(DefaultLimit), MaxLimit);
        if (limit == 0)
        {
            limit = DefaultLimit;
        }

        string? assetCategory = null;
        if (!string.IsNullOrWhiteSpace(request.AssetCategory)
            && !AssetCategoryCatalog.TryNormalize(request.AssetCategory, out assetCategory))
        {
            throw new FusedMaintenanceQueryValidationException(
                $"Asset category '{request.AssetCategory}' is not supported.");
        }

        var building = NormalizeMetadataFilter(request.Building, nameof(request.Building));
        var department = NormalizeMetadataFilter(request.Department, nameof(request.Department));
        var location = NormalizeMetadataFilter(request.Location, nameof(request.Location));

        if (request.DateFrom is not null
            && request.DateTo is not null
            && request.DateFrom > request.DateTo)
        {
            throw new FusedMaintenanceQueryValidationException("DateFrom cannot be later than DateTo.");
        }

        return new FusedMaintenanceQuery(
            normalizedQuery,
            limit,
            Math.Min(Math.Max(limit, DefaultCandidateDepth), MaxLimit),
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
            throw new FusedMaintenanceQueryValidationException(
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

internal sealed record FusedMaintenanceQuery(
    string NormalizedQuery,
    int Limit,
    int CandidateLimit,
    Guid? AssetId,
    string? AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    bool? IsOperational,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo);
