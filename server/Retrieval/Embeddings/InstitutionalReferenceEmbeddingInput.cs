using System.Security.Cryptography;
using System.Text;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Retrieval;

internal static class InstitutionalReferenceEmbeddingInput
{
    internal const string InputFormatVersion = "reference-document-section-v1";

    internal static string BuildProfile(EmbeddingServiceDescriptor descriptor)
        => string.Join(
            ':',
            EmbeddingOptions.ProviderAdapterKey,
            descriptor.ProviderKey,
            descriptor.ModelKey,
            InputFormatVersion,
            descriptor.Dimensions?.ToString() ?? "unknown");

    internal static string BuildDocumentInput(ReferenceDocumentSection section, ReferenceDocument document)
        => Normalize(string.Join('\n', document.Title.Trim(), section.Heading.Trim(), section.SectionText));

    internal static string BuildQueryInput(string normalizedQuery)
        => Normalize(normalizedQuery);

    internal static string ComputeSectionSourceHash(ReferenceDocumentSection section, ReferenceDocument document)
    {
        var input = $"{InputFormatVersion}\n{BuildDocumentInput(section, document)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private static string Normalize(string value)
        => value.Normalize(NormalizationForm.FormKC)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
}
