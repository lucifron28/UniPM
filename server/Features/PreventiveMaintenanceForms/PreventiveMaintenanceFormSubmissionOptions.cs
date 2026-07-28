using System.Globalization;
using Microsoft.Extensions.Options;

namespace UniPM.Api.Features.PreventiveMaintenanceForms;

/// <summary>
/// Configures the provisional file-number format assigned when a draft form is submitted.
/// The final institutional file-number policy remains subject to GSD confirmation.
/// </summary>
public sealed class PreventiveMaintenanceFormSubmissionOptions
{
    public const string SectionName = "PreventiveMaintenanceForms:Submission";

    public string ProvisionalFileNumberPrefix { get; set; } = "PMF";
    public int ProvisionalFileNumberSequenceDigits { get; set; } = 4;
}

internal sealed class PreventiveMaintenanceFileNumberGenerator(
    IOptions<PreventiveMaintenanceFormSubmissionOptions> optionsAccessor)
{
    private readonly PreventiveMaintenanceFormSubmissionOptions options = optionsAccessor.Value;

    internal string CreateSeriesPrefix(DateTimeOffset submittedAt)
    {
        var prefix = options.ProvisionalFileNumberPrefix?.Trim().ToUpperInvariant() ?? string.Empty;
        if (prefix.Length is < 1 or > 16 || prefix.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "PreventiveMaintenanceForms:Submission:ProvisionalFileNumberPrefix must use 1 to 16 ASCII letters or digits.");
        }

        if (options.ProvisionalFileNumberSequenceDigits is < 1 or > 8)
        {
            throw new InvalidOperationException(
                "PreventiveMaintenanceForms:Submission:ProvisionalFileNumberSequenceDigits must be between 1 and 8.");
        }

        return string.Concat(
            prefix,
            "-",
            submittedAt.Year.ToString("D4", CultureInfo.InvariantCulture),
            "-");
    }

    internal string CreateNext(string seriesPrefix, IEnumerable<string> existingFileNumbers)
    {
        var largestSequence = 0;
        foreach (var fileNumber in existingFileNumbers)
        {
            if (!TryParseSequence(seriesPrefix, fileNumber, out var sequence))
            {
                continue;
            }

            largestSequence = Math.Max(largestSequence, sequence);
        }

        if (largestSequence == int.MaxValue)
        {
            throw new InvalidOperationException("The provisional file-number sequence is exhausted.");
        }

        var nextSequence = largestSequence + 1;
        return string.Concat(
            seriesPrefix,
            nextSequence.ToString(
                $"D{options.ProvisionalFileNumberSequenceDigits}",
                CultureInfo.InvariantCulture));
    }

    private bool TryParseSequence(string seriesPrefix, string fileNumber, out int sequence)
    {
        sequence = 0;
        if (!fileNumber.StartsWith(seriesPrefix, StringComparison.Ordinal)
            || fileNumber.Length != seriesPrefix.Length + options.ProvisionalFileNumberSequenceDigits)
        {
            return false;
        }

        return int.TryParse(
            fileNumber[seriesPrefix.Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out sequence);
    }
}
