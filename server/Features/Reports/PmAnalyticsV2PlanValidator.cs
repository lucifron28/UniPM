using System.Globalization;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Reports;

internal sealed record PmAnalyticsV2NormalizedPlan(
    PmAnalyticsV2Plan Plan,
    string AssetCategory,
    IReadOnlyList<string> Cycles);

internal static class PmAnalyticsV2PlanValidator
{
    private const int SchemaVersion = 2;
    private const int MaximumRankingLimit = 10;

    internal static bool TryNormalize(
        PmAnalyticsV2Plan? candidate,
        out PmAnalyticsV2NormalizedPlan normalized,
        out Dictionary<string, string[]> errors)
    {
        normalized = null!;
        errors = [];

        if (candidate is null)
        {
            AddError(errors, "plan", "A version 2 analytics plan is required.");
            return false;
        }

        if (candidate.SchemaVersion != SchemaVersion)
        {
            AddError(errors, nameof(candidate.SchemaVersion), "SchemaVersion must be 2.");
        }

        if (!Enum.IsDefined(candidate.Metric))
        {
            AddError(errors, nameof(candidate.Metric), "Metric is not supported.");
        }

        if (!Enum.IsDefined(candidate.Presentation))
        {
            AddError(errors, nameof(candidate.Presentation), "Presentation is not supported.");
        }

        if (!Enum.IsDefined(candidate.CountBasis))
        {
            AddError(errors, nameof(candidate.CountBasis), "CountBasis is not supported.");
        }

        if (!Enum.IsDefined(candidate.GroupBy))
        {
            AddError(errors, nameof(candidate.GroupBy), "GroupBy is not supported.");
        }

        if (!AssetCategoryCatalog.TryNormalize(candidate.AssetCategory, out var category))
        {
            AddError(errors, nameof(candidate.AssetCategory), "AssetCategory must be one of the four supported CPMP categories.");
        }

        var hasPeriod = candidate.Period is not null;
        var hasComparison = candidate.Comparison is not null;
        IReadOnlyList<string> cycles = [];

        if (hasPeriod == hasComparison)
        {
            AddError(errors, "period", "Provide exactly one standalone period or two-cycle comparison.");
        }
        else if (hasPeriod && category is not null)
        {
            if (!TryGetPeriodCycles(candidate.Period!, category, out cycles, out var periodError))
            {
                AddError(errors, nameof(candidate.Period), periodError);
            }
        }
        else if (hasComparison && category is not null)
        {
            if (!TryNormalizeComparison(candidate.Comparison!, category, out cycles, out var comparisonError))
            {
                AddError(errors, nameof(candidate.Comparison), comparisonError);
            }
        }

        var department = NormalizeContext(candidate.Department, nameof(candidate.Department), errors, 256);
        var building = NormalizeContext(candidate.Building, nameof(candidate.Building), errors, 256);
        var assetCode = NormalizeContext(candidate.AssetCode, nameof(candidate.AssetCode), errors, 128);

        if (!Enum.IsDefined(candidate.Condition.GetValueOrDefault()) && candidate.Condition is not null)
        {
            AddError(errors, nameof(candidate.Condition), "Condition is not supported.");
        }

        if (!Enum.IsDefined(candidate.Timeliness.GetValueOrDefault()) && candidate.Timeliness is not null)
        {
            AddError(errors, nameof(candidate.Timeliness), "Timeliness is not supported.");
        }

        if (candidate.Presentation == PmAnalyticsV2Presentation.Percent
            && candidate.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets)
        {
            AddError(errors, nameof(candidate.CountBasis), "Percent results use the AssetCycles denominator; DistinctAssets is only valid for count and record requests.");
        }

        if (candidate.Presentation == PmAnalyticsV2Presentation.Percent
            && (candidate.Condition is not null || candidate.Timeliness is not null))
        {
            AddError(errors, "filters", "Outcome filters cannot be combined with a percentage because they would change its denominator.");
        }

        AddContradictoryFilterErrors(candidate, errors);

        if (candidate.Ranking is not null)
        {
            if (candidate.GroupBy != PmAnalyticsV2GroupBy.Department)
            {
                AddError(errors, nameof(candidate.GroupBy), "Department ranking requires GroupBy Department.");
            }

            if (candidate.Comparison is not null)
            {
                AddError(errors, nameof(candidate.Ranking), "Ranking and comparison cannot be combined.");
            }

            if (candidate.Ranking.Limit is < 1 or > MaximumRankingLimit)
            {
                AddError(errors, "ranking.limit", $"Ranking Limit must be between 1 and {MaximumRankingLimit}.");
            }

            if (!Enum.IsDefined(candidate.Ranking.Direction))
            {
                AddError(errors, "ranking.direction", "Ranking Direction must be Highest or Lowest.");
            }

            if (candidate.Presentation == PmAnalyticsV2Presentation.Records)
            {
                AddError(errors, nameof(candidate.Presentation), "Department ranking supports Count or Percent presentation.");
            }
        }

        if (candidate.Comparison is not null && candidate.GroupBy != PmAnalyticsV2GroupBy.None)
        {
            AddError(errors, nameof(candidate.GroupBy), "Two-cycle comparison does not support grouping in this release.");
        }

        if (errors.Count > 0 || category is null || cycles.Count == 0)
        {
            return false;
        }

        var plan = candidate with
        {
            AssetCategory = category,
            Department = department,
            Building = building,
            AssetCode = assetCode
        };
        normalized = new PmAnalyticsV2NormalizedPlan(plan, category, cycles);
        return true;
    }

    private static bool TryGetPeriodCycles(
        PmAnalyticsV2Period period,
        string category,
        out IReadOnlyList<string> cycles,
        out string error)
    {
        cycles = [];
        error = "Period fields do not match the selected period kind.";

        int startYear;
        int startMonth;
        int endYear;
        int endMonth;

        switch (period.Kind)
        {
            case PmAnalyticsV2PeriodKind.Month:
                if (!HasOnly(period, allowYear: true, allowMonth: true)
                    || !TryValidateYear(period.Year, out startYear)
                    || period.Month is null
                    || period.Month.Value is < 1 or > 12)
                {
                    error = "Month periods require a supported Year and Month from 1 through 12 only.";
                    return false;
                }

                startMonth = endMonth = period.Month.Value;
                endYear = startYear;
                if (!CpmpScheduleFrequency.IsValid(category, startMonth))
                {
                    error = "The selected single month is not a CPMP schedule month for this category.";
                    return false;
                }

                cycles = [FormatCycle(startYear, startMonth)];
                error = string.Empty;
                return true;

            case PmAnalyticsV2PeriodKind.Quarter:
                if (!HasOnly(period, allowYear: true, allowQuarter: true)
                    || !TryValidateYear(period.Year, out startYear)
                    || period.Quarter is null
                    || period.Quarter.Value is < 1 or > 4)
                {
                    error = "Quarter periods require a supported Year and Quarter from 1 through 4 only.";
                    return false;
                }

                startMonth = (period.Quarter.Value - 1) * 3 + 1;
                endYear = startYear;
                endMonth = startMonth + 2;
                break;

            case PmAnalyticsV2PeriodKind.Year:
                if (!HasOnly(period, allowYear: true) || !TryValidateYear(period.Year, out startYear))
                {
                    error = "Year periods require a supported Year and no other period fields.";
                    return false;
                }

                startMonth = 1;
                endYear = startYear;
                endMonth = 12;
                break;

            case PmAnalyticsV2PeriodKind.Range:
                if (!HasOnly(period, allowRange: true)
                    || !TryParseCycle(period.StartCycle, out startYear, out startMonth)
                    || !TryParseCycle(period.EndCycle, out endYear, out endMonth))
                {
                    error = "Range periods require canonical StartCycle and EndCycle values in yyyy-MM form only.";
                    return false;
                }

                break;

            default:
                error = "Period Kind is not supported.";
                return false;
        }

        var span = (endYear - startYear) * 12 + endMonth - startMonth + 1;
        if (span is < 1 or > 12)
        {
            error = "A period range must be ordered and contain no more than 12 calendar months.";
            return false;
        }

        var scheduledMonths = CpmpScheduleFrequency.GetMonths(category).ToHashSet();
        var selected = new List<string>();
        for (var offset = 0; offset < span; offset++)
        {
            var date = new DateTime(startYear, startMonth, 1).AddMonths(offset);
            if (scheduledMonths.Contains(date.Month))
            {
                selected.Add(FormatCycle(date.Year, date.Month));
            }
        }

        if (selected.Count == 0)
        {
            error = "The selected period contains no CPMP schedule cycles for this category.";
            return false;
        }

        cycles = selected;
        error = string.Empty;
        return true;
    }

    private static bool TryNormalizeComparison(
        PmAnalyticsV2Comparison comparison,
        string category,
        out IReadOnlyList<string> cycles,
        out string error)
    {
        cycles = [];
        error = "Comparison requires two distinct, ordered CPMP cycles for the same category.";
        if (!TryParseCycle(comparison.EarlierCycle, out _, out var earlierMonth)
            || !TryParseCycle(comparison.LaterCycle, out _, out var laterMonth)
            || string.CompareOrdinal(comparison.EarlierCycle, comparison.LaterCycle) >= 0
            || !CpmpScheduleFrequency.IsValid(category, earlierMonth)
            || !CpmpScheduleFrequency.IsValid(category, laterMonth))
        {
            return false;
        }

        cycles = [comparison.EarlierCycle, comparison.LaterCycle];
        error = string.Empty;
        return true;
    }

    private static bool HasOnly(
        PmAnalyticsV2Period period,
        bool allowYear = false,
        bool allowMonth = false,
        bool allowQuarter = false,
        bool allowRange = false)
    {
        return (allowYear || period.Year is null)
            && (allowMonth || period.Month is null)
            && (allowQuarter || period.Quarter is null)
            && (allowRange || (period.StartCycle is null && period.EndCycle is null));
    }

    private static bool TryValidateYear(int? value, out int year)
    {
        year = value.GetValueOrDefault();
        return value is >= 2000 and <= 9998;
    }

    private static bool TryParseCycle(string? value, out int year, out int month)
    {
        return PreventiveMaintenanceCycle.TryParse(value, out year, out month)
            && string.Equals(value, FormatCycle(year, month), StringComparison.Ordinal)
            && year is >= 2000 and <= 9998;
    }

    private static string FormatCycle(int year, int month) =>
        year.ToString("D4", CultureInfo.InvariantCulture) + "-" + month.ToString("D2", CultureInfo.InvariantCulture);

    private static string? NormalizeContext(
        string? value,
        string field,
        Dictionary<string, string[]> errors,
        int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            AddError(errors, field, $"{field} must be non-empty, at most {maximumLength} characters, and contain no control characters.");
            return null;
        }

        return normalized;
    }

    private static void AddContradictoryFilterErrors(
        PmAnalyticsV2Plan plan,
        Dictionary<string, string[]> errors)
    {
        var condition = plan.Condition;
        var timeliness = plan.Timeliness;
        var metric = plan.Metric;

        if (metric == PmAnalyticsV2Metric.NonOperational
            && (condition is PmAnalyticsV2Condition.Operational or PmAnalyticsV2Condition.NotInspected))
        {
            AddError(errors, nameof(plan.Condition), "NonOperational results cannot match Operational or NotInspected condition filters.");
        }

        if ((metric is PmAnalyticsV2Metric.Progress
                or PmAnalyticsV2Metric.OnTimeCompliance
                or PmAnalyticsV2Metric.CompletedLate)
            && condition == PmAnalyticsV2Condition.NotInspected)
        {
            AddError(errors, nameof(plan.Condition), "This metric requires a completed inspection and cannot use the NotInspected condition.");
        }

        if ((metric is PmAnalyticsV2Metric.Remaining or PmAnalyticsV2Metric.Missed)
            && (condition is PmAnalyticsV2Condition.Operational or PmAnalyticsV2Condition.NonOperational))
        {
            AddError(errors, nameof(plan.Condition), "Remaining and Missed results have no completed operational condition.");
        }

        var impossibleTimeliness = metric switch
        {
            PmAnalyticsV2Metric.Progress => timeliness is PmAnalyticsV2Timeliness.Remaining
                or PmAnalyticsV2Timeliness.Missed or PmAnalyticsV2Timeliness.Future,
            PmAnalyticsV2Metric.OnTimeCompliance => timeliness is PmAnalyticsV2Timeliness.Late
                or PmAnalyticsV2Timeliness.Remaining or PmAnalyticsV2Timeliness.Missed or PmAnalyticsV2Timeliness.Future,
            PmAnalyticsV2Metric.CompletedLate => timeliness is PmAnalyticsV2Timeliness.OnTime
                or PmAnalyticsV2Timeliness.Remaining or PmAnalyticsV2Timeliness.Missed or PmAnalyticsV2Timeliness.Future,
            PmAnalyticsV2Metric.Remaining => timeliness is PmAnalyticsV2Timeliness.OnTime
                or PmAnalyticsV2Timeliness.Late or PmAnalyticsV2Timeliness.Missed or PmAnalyticsV2Timeliness.Future,
            PmAnalyticsV2Metric.Missed => timeliness is PmAnalyticsV2Timeliness.OnTime
                or PmAnalyticsV2Timeliness.Late or PmAnalyticsV2Timeliness.Remaining or PmAnalyticsV2Timeliness.Future,
            _ => false
        };

        if (impossibleTimeliness)
        {
            AddError(errors, nameof(plan.Timeliness), $"{timeliness} cannot match the {metric} metric.");
        }

        if ((timeliness is PmAnalyticsV2Timeliness.Remaining
                or PmAnalyticsV2Timeliness.Missed
                or PmAnalyticsV2Timeliness.Future)
            && (condition is PmAnalyticsV2Condition.Operational or PmAnalyticsV2Condition.NonOperational))
        {
            AddError(errors, nameof(plan.Condition), "Unfinished and future cycles do not have an operational condition.");
        }

        if (condition == PmAnalyticsV2Condition.NotInspected
            && (timeliness is PmAnalyticsV2Timeliness.OnTime or PmAnalyticsV2Timeliness.Late))
        {
            AddError(errors, nameof(plan.Timeliness), "NotInspected cannot be combined with OnTime or Late.");
        }

        if (metric == PmAnalyticsV2Metric.NonOperational
            && (timeliness is PmAnalyticsV2Timeliness.Remaining
                or PmAnalyticsV2Timeliness.Missed
                or PmAnalyticsV2Timeliness.Future))
        {
            AddError(errors, nameof(plan.Timeliness), "NonOperational results require a completed inspection.");
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var existing))
        {
            errors.Add(key, [message]);
            return;
        }

        errors[key] = [.. existing, message];
    }
}
