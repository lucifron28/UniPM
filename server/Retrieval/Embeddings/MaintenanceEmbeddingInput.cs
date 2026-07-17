using System.Security.Cryptography;
using System.Text;

namespace UniPM.Api.Features.Retrieval;

internal static class MaintenanceEmbeddingInput
{
    internal const string InputFormatVersion = "maintenance-search-document-embedding-v1";

    public static string NormalizeDocumentText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Normalize(NormalizationForm.FormKC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    public static string ComputeSourceHash(string searchText)
    {
        var normalized = NormalizeDocumentText(searchText);
        var input = $"{InputFormatVersion}\n{normalized}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public static string BuildQueryInput(
        string normalizedQuery,
        IReadOnlyList<string> issueKeys)
    {
        if (issueKeys.Count == 0)
        {
            return normalizedQuery;
        }

        return string.Join(
            '\n',
            normalizedQuery,
            $"issue-context: {string.Join(' ', issueKeys)}");
    }
}
