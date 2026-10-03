using System.Globalization;
using System.Text.RegularExpressions;

namespace UniPM.Api.Features.Reports;

internal static class PmAnalyticsQuestionParser
{
    private const int MaximumQuestionLength = 512;

    private static readonly Regex Template = new(
        @"\AShow\s+(?<metric>progress|on-time compliance|late inspections|non-operational assets)\s+for\s+(?<category>fire-extinguisher|fire extinguishers|fire-alarm|fire alarms|fire alarm systems|emergency-light|emergency lights|water-drinking-station|water drinking stations)\s+in\s+(?<cycle>[0-9]{4}-[0-9]{2}|[\p{L}]+\s+[0-9]{4})(?:\s+department\s+""(?<department>[^""]*)"")?(?<group>\s+grouped by department)?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal static bool TryParse(
        string? question,
        out PmAnalyticsPlan? plan,
        out string error)
    {
        plan = null;
        error = "Question must match the supported PM analytics template.";

        if (string.IsNullOrWhiteSpace(question)
            || question.Length > MaximumQuestionLength
            || question.Any(char.IsControl))
        {
            return false;
        }

        Match match;
        try
        {
            match = Template.Match(question.Trim());
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        if (!match.Success)
        {
            return false;
        }

        if (!TryNormalizeCycle(match.Groups["cycle"].Value, out var pmCycle))
        {
            return false;
        }

        var metric = match.Groups["metric"].Value.ToLowerInvariant() switch
        {
            "progress" => PmAnalyticsMetric.Progress,
            "on-time compliance" => PmAnalyticsMetric.OnTimeCompliance,
            "late inspections" => PmAnalyticsMetric.CompletedLate,
            "non-operational assets" => PmAnalyticsMetric.NonOperational,
            _ => (PmAnalyticsMetric?)null
        };

        var assetCategory = NormalizeCategory(match.Groups["category"].Value);
        if (metric is null || assetCategory is null)
        {
            return false;
        }

        var department = match.Groups["department"].Success
            ? match.Groups["department"].Value
            : null;
        plan = new PmAnalyticsPlan(
            metric.Value,
            assetCategory,
            pmCycle,
            department,
            match.Groups["group"].Success
                ? PmAnalyticsGroupBy.Department
                : PmAnalyticsGroupBy.None);
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeCycle(string value, out string pmCycle)
    {
        pmCycle = string.Empty;
        if (value.Contains('-', StringComparison.Ordinal))
        {
            pmCycle = value;
            return true;
        }

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        if (!DateTime.TryParseExact(
                normalized,
                "MMMM yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        pmCycle = parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return true;
    }

    private static string? NormalizeCategory(string value)
    {
        if (value.Equals("fire-extinguisher", StringComparison.OrdinalIgnoreCase)
            || value.Equals("fire extinguishers", StringComparison.OrdinalIgnoreCase))
        {
            return "fire-extinguisher";
        }

        if (value.Equals("fire-alarm", StringComparison.OrdinalIgnoreCase)
            || value.Equals("fire alarms", StringComparison.OrdinalIgnoreCase)
            || value.Equals("fire alarm systems", StringComparison.OrdinalIgnoreCase))
        {
            return "fire-alarm";
        }

        if (value.Equals("emergency-light", StringComparison.OrdinalIgnoreCase)
            || value.Equals("emergency lights", StringComparison.OrdinalIgnoreCase))
        {
            return "emergency-light";
        }

        if (value.Equals("water-drinking-station", StringComparison.OrdinalIgnoreCase)
            || value.Equals("water drinking stations", StringComparison.OrdinalIgnoreCase))
        {
            return "water-drinking-station";
        }

        return null;
    }
}
