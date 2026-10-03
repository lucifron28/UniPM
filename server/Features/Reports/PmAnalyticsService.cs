using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Reports;

internal sealed class PmAnalyticsService(PmPeriodDashboardService dashboardService)
{
    private const int MaximumSources = 100;
    private const string ScopeNote =
        "These metrics use live dashboard completion, including Draft and Submitted form rows; they are not acknowledged-only official history. Cancelled schedules are excluded. PM cycle deadlines use Asia/Manila (UTC+08:00).";

    internal async Task<PmAnalyticsExecutionResult> ExecuteAsync(
        PmAnalyticsPlan? candidate,
        CancellationToken cancellationToken)
    {
        if (!PmAnalyticsPlanValidator.TryNormalize(candidate, out var plan, out var error))
        {
            return new PmAnalyticsExecutionResult(null, error);
        }

        var dashboard = await dashboardService.GetPeriodAsync(
            new PmPeriodDashboardQuery(
                plan.PmCycle,
                plan.AssetCategory,
                plan.Department,
                null,
                null,
                null),
            cancellationToken);

        var groups = plan.GroupBy == PmAnalyticsGroupBy.Department
            ? dashboard.Batches
                .Select(batch => ToGroupMeasure(plan.Metric, batch, dashboard.Assets))
                .ToArray()
            : Array.Empty<PmAnalyticsMeasureResponse>();

        var orderedSources = dashboard.Assets
            .OrderBy(row => row.AssetCode, StringComparer.Ordinal)
            .ThenBy(row => row.ScheduleId)
            .ToArray();
        var sources = orderedSources
            .Take(MaximumSources)
            .Select(row => new PmAnalyticsSourceResponse(
                row.ScheduleId,
                row.AssetId,
                row.InspectionId,
                row.AssetCode,
                row.Department,
                row.PmCycle,
                row.Deadline,
                row.InspectionCompletedAt,
                row.Timeliness,
                row.Condition,
                row.FormStatus))
            .ToArray();

        return new PmAnalyticsExecutionResult(
            new PmAnalyticsResponse(
                new PmAnalyticsPlanResponse(
                    plan.Metric.ToString(),
                    plan.AssetCategory,
                    plan.PmCycle,
                    plan.Department,
                    plan.GroupBy.ToString()),
                dashboard.Deadline,
                dashboard.PeriodState,
                ToOverallMeasure(plan.Metric, dashboard, plan.Department),
                groups,
                sources,
                orderedSources.Length,
                orderedSources.Length > MaximumSources,
                ScopeNote),
            null);
    }

    private static PmAnalyticsMeasureResponse ToOverallMeasure(
        PmAnalyticsMetric metric,
        PmPeriodDashboardResponse dashboard,
        string? department)
    {
        return metric switch
        {
            PmAnalyticsMetric.Progress => new PmAnalyticsMeasureResponse(
                department,
                dashboard.Inspected,
                dashboard.Scheduled,
                dashboard.ProgressPercent,
                "Percent",
                dashboard.Scheduled > 0),
            PmAnalyticsMetric.OnTimeCompliance => new PmAnalyticsMeasureResponse(
                department,
                dashboard.CompletedOnTime,
                dashboard.Scheduled,
                dashboard.OnTimeCompliancePercent,
                "Percent",
                dashboard.OnTimeCompliancePercent is not null),
            PmAnalyticsMetric.CompletedLate => CountMeasure(
                department,
                dashboard.CompletedLate,
                dashboard.Scheduled),
            PmAnalyticsMetric.NonOperational => CountMeasure(
                department,
                dashboard.NonOperational,
                dashboard.Scheduled),
            _ => throw new InvalidOperationException("Validated analytics metric was not supported.")
        };
    }

    private static PmAnalyticsMeasureResponse ToGroupMeasure(
        PmAnalyticsMetric metric,
        PmPeriodDashboardBatchResponse batch,
        IReadOnlyList<PmPeriodDashboardAssetRowResponse> assets)
    {
        return metric switch
        {
            PmAnalyticsMetric.Progress => new PmAnalyticsMeasureResponse(
                batch.Department,
                batch.Inspected,
                batch.Scheduled,
                ToPercent(batch.Inspected, batch.Scheduled),
                "Percent",
                batch.Scheduled > 0),
            PmAnalyticsMetric.OnTimeCompliance => new PmAnalyticsMeasureResponse(
                batch.Department,
                batch.CompletedOnTime,
                batch.Scheduled,
                batch.OnTimeCompliancePercent,
                "Percent",
                batch.OnTimeCompliancePercent is not null),
            PmAnalyticsMetric.CompletedLate => CountMeasure(
                batch.Department,
                batch.CompletedLate,
                batch.Scheduled),
            PmAnalyticsMetric.NonOperational => CountMeasure(
                batch.Department,
                assets.Count(row => string.Equals(
                    row.Department,
                    batch.Department,
                    StringComparison.Ordinal)
                    && row.Condition == PmPeriodDashboardFilterCatalog.NonOperational),
                batch.Scheduled),
            _ => throw new InvalidOperationException("Validated analytics metric was not supported.")
        };
    }

    private static PmAnalyticsMeasureResponse CountMeasure(
        string? department,
        int count,
        int scheduled)
    {
        return new PmAnalyticsMeasureResponse(
            department,
            count,
            scheduled,
            count,
            "Count",
            true);
    }

    private static decimal ToPercent(int numerator, int denominator)
    {
        return denominator == 0
            ? 0
            : Math.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
    }
}

internal static class PmAnalyticsPlanValidator
{
    internal static bool TryNormalize(
        PmAnalyticsPlan? candidate,
        out PmAnalyticsPlan plan,
        out string error)
    {
        plan = null!;
        error = "The analytics plan is not supported.";

        if (candidate is null
            || candidate.Metric is not (PmAnalyticsMetric.Progress
                or PmAnalyticsMetric.OnTimeCompliance
                or PmAnalyticsMetric.CompletedLate
                or PmAnalyticsMetric.NonOperational)
            || candidate.GroupBy is not (PmAnalyticsGroupBy.None or PmAnalyticsGroupBy.Department)
            || !AssetCategoryCatalog.TryNormalize(candidate.AssetCategory, out var assetCategory)
            || !PreventiveMaintenanceCycle.TryParse(candidate.PmCycle, out var year, out var month)
            || !string.Equals(
                candidate.PmCycle,
                $"{year:D4}-{month:D2}",
                StringComparison.Ordinal)
            || !CpmpScheduleFrequency.IsValid(assetCategory, month)
            || (year == 9999 && month == 12))
        {
            return false;
        }

        string? department = null;
        if (candidate.Department is not null)
        {
            department = candidate.Department.Trim();
            if (department.Length == 0
                || department.Length > 256
                || department.Any(char.IsControl))
            {
                return false;
            }

            department = department.ToUpperInvariant();
        }

        plan = new PmAnalyticsPlan(
            candidate.Metric,
            assetCategory,
            candidate.PmCycle,
            department,
            candidate.GroupBy);
        error = string.Empty;
        return true;
    }
}
