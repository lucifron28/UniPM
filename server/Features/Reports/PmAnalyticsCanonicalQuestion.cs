namespace UniPM.Api.Features.Reports;

internal static class PmAnalyticsCanonicalQuestion
{
    internal static bool TryCreate(PmAnalyticsPlan plan, out string canonicalQuestion)
    {
        canonicalQuestion = string.Empty;
        if (!PmAnalyticsPlanValidator.TryNormalize(plan, out var normalized, out _))
        {
            return false;
        }

        var metric = normalized.Metric switch
        {
            PmAnalyticsMetric.Progress => "progress",
            PmAnalyticsMetric.OnTimeCompliance => "on-time compliance",
            PmAnalyticsMetric.CompletedLate => "late inspections",
            PmAnalyticsMetric.NonOperational => "non-operational assets",
            _ => null
        };
        if (metric is null)
        {
            return false;
        }

        var parts = new List<string>
        {
            "Show",
            metric,
            "for",
            normalized.AssetCategory,
            "in",
            normalized.PmCycle
        };

        if (normalized.Department is not null)
        {
            if (!IsSafeDepartment(normalized.Department))
            {
                return false;
            }

            parts.Add("department");
            parts.Add($"\"{normalized.Department}\"");
        }

        if (normalized.GroupBy == PmAnalyticsGroupBy.Department)
        {
            parts.Add("grouped by department");
        }

        canonicalQuestion = string.Join(' ', parts);
        return canonicalQuestion.Length <= 512
            && PmAnalyticsQuestionParser.TryParse(canonicalQuestion, out var parsed, out _)
            && PmAnalyticsPlanValidator.TryNormalize(parsed, out var normalizedParsed, out _)
            && normalizedParsed == normalized;
    }

    private static bool IsSafeDepartment(string department)
    {
        return department.Length is > 0 and <= 256
            && !department.Any(char.IsControl)
            && !department.Contains('"')
            && !department.Contains('\\')
            && department.All(character => char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character is '&' or '-' or '.' or '\'');
    }
}
