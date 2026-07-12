using System.Text.RegularExpressions;

namespace UniPM.Api.Features.MaintenanceReview;

internal static class SummaryOutputValidator
{
    private static readonly Regex CitationPattern = new(
        @"\[(SRC-[0-9]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Validate(
        string output,
        IReadOnlySet<string> includedSourceLabels,
        int maxOutputCharacters)
    {
        if (string.IsNullOrWhiteSpace(output) || output.Length > maxOutputCharacters)
        {
            throw new SummaryServiceDataException("The generated summary is outside the configured bounds.");
        }

        var citations = CitationPattern.Matches(output)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (citations.Length == 0 || citations.Any(label => !includedSourceLabels.Contains(label)))
        {
            throw new SummaryServiceDataException("The generated summary did not contain valid selected-source citations.");
        }

        return output.Trim();
    }
}
