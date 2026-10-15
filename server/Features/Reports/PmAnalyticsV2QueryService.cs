using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Reports;

internal sealed record PmAnalyticsV2PlanResolution(
    PmAnalyticsV2NormalizedPlan? Plan,
    Dictionary<string, string[]> Errors,
    IReadOnlyList<string> ClarificationFields);

internal sealed class PmAnalyticsV2QueryService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
{
    private const int MaximumPageSize = 100;
    private const int MaximumPage = 100_000;
    private const string ScopeNote =
        "V2 is a live PM analytics view. Inspection completion uses CompletedAt, including Draft and Submitted forms; official history remains acknowledged-only. Cancelled schedules are excluded. Deadlines use Asia/Manila (UTC+08:00). Department and building are current Asset fields, not historical snapshots. Blank stored cycle values use ScheduleDate as an explicit fallback; absent schedule cycles are not counted as missed. Ranking limits returned groups only; population totals and source pages retain the full plan scope.";

    internal async Task<PmAnalyticsV2PlanResolution> NormalizeAndResolveAsync(
        PmAnalyticsV2Plan? candidate,
        CancellationToken cancellationToken)
    {
        if (!PmAnalyticsV2PlanValidator.TryNormalize(candidate, out var normalized, out var errors))
        {
            return new PmAnalyticsV2PlanResolution(null, errors, []);
        }

        var plan = normalized.Plan;
        var clarificationFields = new List<string>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var department = await ResolveCatalogValueAsync(
            context,
            plan.Department,
            asset => asset.Department,
            nameof(plan.Department),
            cancellationToken,
            errors,
            clarificationFields);
        var building = await ResolveCatalogValueAsync(
            context,
            plan.Building,
            asset => asset.Building,
            nameof(plan.Building),
            cancellationToken,
            errors,
            clarificationFields);
        var assetCode = await ResolveCatalogValueAsync(
            context,
            plan.AssetCode,
            asset => asset.AssetCode,
            nameof(plan.AssetCode),
            cancellationToken,
            errors,
            clarificationFields);

        if (errors.Count > 0)
        {
            return new PmAnalyticsV2PlanResolution(null, errors, clarificationFields);
        }

        plan = plan with
        {
            Department = department,
            Building = building,
            AssetCode = assetCode
        };
        return new PmAnalyticsV2PlanResolution(
            normalized with { Plan = plan },
            errors,
            clarificationFields);
    }

    internal async Task<PmAnalyticsV2QueryResponse> ExecuteAsync(
        PmAnalyticsV2NormalizedPlan normalized,
        PmAnalyticsV2SourceScope sourceScope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(sourceScope))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceScope));
        }

        if (page is < 1 or > MaximumPage || pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(page), $"Page must be between 1 and {MaximumPage}, and PageSize must be between 1 and {MaximumPageSize}.");
        }

        var now = timeProvider.GetUtcNow();
        var windows = normalized.Cycles.Select(CreateCycleWindow).ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = BuildRows(context, normalized, windows);
        var matchingRows = ApplyMatchingFilters(rows, normalized.Plan, now);

        var cycleAggregates = await LoadCycleAggregatesAsync(rows, now, cancellationToken);
        var eligibleAssetCycles = cycleAggregates.Sum(aggregate => aggregate.AssetCycleCount);
        var eligibleDistinctAssets = await CountDistinctAssetsAsync(rows, cancellationToken);
        var matchingAssetCycles = await matchingRows.CountAsync(cancellationToken);
        var matchingDistinctAssets = await CountDistinctAssetsAsync(matchingRows, cancellationToken);
        var matchingByCycle = await LoadMatchingCycleAggregatesAsync(matchingRows, cancellationToken);

        var selectedCycles = BuildCycleResponses(windows, cycleAggregates, now);
        var openCycleCount = windows.Count(window => window.Deadline >= now);
        var matchingByCycleLookup = matchingByCycle.ToDictionary(
            aggregate => aggregate.PmCycle,
            StringComparer.Ordinal);
        var result = normalized.Plan.Comparison is null
            ? BuildPeriodMeasure(
                normalized.Plan,
                openCycleCount,
                eligibleAssetCycles,
                eligibleDistinctAssets,
                matchingAssetCycles,
                matchingDistinctAssets)
            : null;
        var comparison = normalized.Plan.Comparison is null
            ? null
            : BuildComparison(
                normalized.Plan,
                selectedCycles,
                matchingByCycleLookup);

        var groups = Array.Empty<PmAnalyticsV2GroupResponse>();
        var rankingExclusions = Array.Empty<PmAnalyticsV2RankingExclusionResponse>();
        if (normalized.Plan.GroupBy != PmAnalyticsV2GroupBy.None)
        {
            var groupAggregates = await LoadGroupAggregatesAsync(
                rows,
                matchingRows,
                normalized.Plan.GroupBy,
                cancellationToken);
            (groups, rankingExclusions) = BuildGroups(
                normalized.Plan,
                groupAggregates,
                openCycleCount,
                now,
                windows.ToDictionary(window => window.PmCycle, StringComparer.Ordinal));
        }

        var sourceQuery = sourceScope == PmAnalyticsV2SourceScope.Matching
            ? matchingRows
            : rows;
        var sourceTotalCount = sourceScope == PmAnalyticsV2SourceScope.Matching
            ? matchingAssetCycles
            : eligibleAssetCycles;
        var totalPages = sourceTotalCount == 0
            ? 0
            : (sourceTotalCount + pageSize - 1) / pageSize;
        var offset = checked((page - 1) * pageSize);
        var sources = await sourceQuery
            .OrderBy(row => row.AssetCode)
            .ThenBy(row => row.PmCycle)
            .ThenBy(row => row.ScheduleId)
            .Skip(offset)
            .Take(pageSize)
            .Select(row => new SourceProjection(
                row.ScheduleId,
                row.AssetId,
                row.InspectionId,
                row.AssetCode,
                row.AssetCategory,
                row.CurrentDepartment,
                row.CurrentBuilding,
                row.PmCycle,
                row.CycleStart,
                row.Deadline,
                row.CompletedAt,
                row.ScheduleStatus,
                row.FormStatus,
                row.IsOperational,
                row.UsedLegacyDateFallback))
            .ToListAsync(cancellationToken);

        var planResponse = normalized.Plan;
        var population = new PmAnalyticsV2PopulationResponse(
            eligibleAssetCycles,
            eligibleDistinctAssets,
            matchingAssetCycles,
            matchingDistinctAssets,
            sourceTotalCount,
            totalPages);

        return new PmAnalyticsV2QueryResponse(
            2,
            now,
            planResponse,
            sourceScope,
            page,
            pageSize,
            population,
            result,
            selectedCycles,
            groups,
            rankingExclusions,
            comparison,
            sources.Select(source => ToSourceResponse(source, now)).ToArray(),
            ScopeNote);
    }

    private static async Task<string?> ResolveCatalogValueAsync(
        ApplicationDbContext context,
        string? candidate,
        Expression<Func<Asset, string?>> selector,
        string field,
        CancellationToken cancellationToken,
        Dictionary<string, string[]> errors,
        List<string> clarificationFields)
    {
        if (candidate is null)
        {
            return null;
        }

        var normalizedCandidate = candidate.Trim().ToUpperInvariant();
        var values = await context.Assets
            .AsNoTracking()
            .Select(selector)
            .Where(value => value != null && value.Trim().ToUpper() == normalizedCandidate)
            .Select(value => value!.Trim())
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);
        var matches = values.ToArray();
        if (matches.Length == 1)
        {
            return matches[0].ToUpperInvariant();
        }

        var reason = matches.Length == 0
            ? "The value does not match the current asset catalog."
            : "The value matches more than one current catalog spelling.";
        errors[field] = [reason];
        clarificationFields.Add(field);
        return null;
    }

    private static IQueryable<PmAnalyticsV2SqlRow> BuildRows(
        ApplicationDbContext context,
        PmAnalyticsV2NormalizedPlan normalized,
        IReadOnlyList<CycleWindow> windows)
    {
        IQueryable<ScheduleRow> input =
            from schedule in context.PreventiveMaintenanceSchedules.AsNoTracking()
            join asset in context.Assets.AsNoTracking() on schedule.AssetId equals asset.Id
            where schedule.Status != ScheduleStatusCatalog.Cancelled
                && asset.AssetCategory == normalized.AssetCategory
                && (normalized.Plan.Department == null
                    || asset.Department != null && asset.Department.Trim().ToUpper() == normalized.Plan.Department)
                && (normalized.Plan.Building == null
                    || asset.Building != null && asset.Building.Trim().ToUpper() == normalized.Plan.Building)
                && (normalized.Plan.AssetCode == null
                    || asset.AssetCode.Trim().ToUpper() == normalized.Plan.AssetCode)
            select new ScheduleRow
            {
                ScheduleId = schedule.Id,
                AssetId = schedule.AssetId,
                StoredPmCycle = schedule.PmCycle,
                ScheduleDate = schedule.ScheduleDate,
                ScheduleStatus = schedule.Status,
                AssetCode = asset.AssetCode,
                AssetCategory = asset.AssetCategory,
                CurrentDepartment = asset.Department,
                CurrentBuilding = asset.Building
            };

        var cycleProjection = BuildCycleProjection(windows);
        var effectiveCycles = windows.Select(window => window.PmCycle).ToArray();
        var selected = input
            .Select(cycleProjection)
            .Where(row => effectiveCycles.Contains(row.EffectivePmCycle!));
        var timed = selected.Select(BuildTimeProjection(windows));

        return
            from schedule in timed
            join inspection in context.InspectionRecords.AsNoTracking()
                on schedule.ScheduleId equals inspection.ScheduleId into inspections
            from inspection in inspections.DefaultIfEmpty()
            select new PmAnalyticsV2SqlRow
            {
                ScheduleId = schedule.ScheduleId,
                AssetId = schedule.AssetId,
                AssetCode = schedule.AssetCode,
                AssetCategory = schedule.AssetCategory,
                CurrentDepartment = schedule.CurrentDepartment,
                CurrentBuilding = schedule.CurrentBuilding,
                PmCycle = schedule.EffectivePmCycle!,
                ScheduleStatus = schedule.ScheduleStatus,
                ScheduleDate = schedule.ScheduleDate,
                CycleStart = schedule.CycleStart,
                Deadline = schedule.Deadline,
                UsedLegacyDateFallback = schedule.UsedLegacyDateFallback,
                InspectionId = inspection == null ? null : (Guid?)inspection.Id,
                CompletedAt = inspection == null ? null : inspection.CompletedAt,
                IsOperational = inspection == null ? null : (bool?)inspection.IsOperational,
                FormStatus = inspection == null || inspection.PreventiveMaintenanceForm == null
                    ? null
                    : inspection.PreventiveMaintenanceForm.Status
            };
    }

    private static Expression<Func<ScheduleRow, ScheduleRow>> BuildCycleProjection(
        IReadOnlyList<CycleWindow> windows)
    {
        var row = Expression.Parameter(typeof(ScheduleRow), "row");
        var storedCycle = Expression.Property(row, nameof(ScheduleRow.StoredPmCycle));
        var scheduleDate = Expression.Property(row, nameof(ScheduleRow.ScheduleDate));
        Expression fallbackCycle = Expression.Constant(null, typeof(string));
        foreach (var window in windows.Reverse())
        {
            var inWindow = Expression.AndAlso(
                Expression.GreaterThanOrEqual(scheduleDate, Expression.Constant(window.Start)),
                Expression.LessThan(scheduleDate, Expression.Constant(window.NextStart)));
            fallbackCycle = Expression.Condition(
                inWindow,
                Expression.Constant(window.PmCycle),
                fallbackCycle);
        }

        var trimmedStoredCycle = Expression.Condition(
            Expression.Equal(storedCycle, Expression.Constant(null, typeof(string))),
            Expression.Constant(null, typeof(string)),
            Expression.Call(storedCycle, typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!));
        var storedCycleMissing = Expression.OrElse(
            Expression.Equal(trimmedStoredCycle, Expression.Constant(null, typeof(string))),
            Expression.Equal(trimmedStoredCycle, Expression.Constant(string.Empty)));
        var effectiveCycle = Expression.Condition(storedCycleMissing, fallbackCycle, trimmedStoredCycle);
        var usedFallback = Expression.Condition(
            storedCycleMissing,
            Expression.NotEqual(fallbackCycle, Expression.Constant(null, typeof(string))),
            Expression.Constant(false));
        var bindings = CopyScheduleBindings(row,
            nameof(ScheduleRow.StoredPmCycle),
            nameof(ScheduleRow.EffectivePmCycle),
            nameof(ScheduleRow.UsedLegacyDateFallback),
            nameof(ScheduleRow.CycleStart),
            nameof(ScheduleRow.Deadline));
        bindings.Add(Expression.Bind(
            typeof(ScheduleRow).GetProperty(nameof(ScheduleRow.StoredPmCycle))!,
            trimmedStoredCycle));
        bindings.Add(Expression.Bind(
            typeof(ScheduleRow).GetProperty(nameof(ScheduleRow.EffectivePmCycle))!,
            effectiveCycle));
        bindings.Add(Expression.Bind(
            typeof(ScheduleRow).GetProperty(nameof(ScheduleRow.UsedLegacyDateFallback))!,
            usedFallback));
        return Expression.Lambda<Func<ScheduleRow, ScheduleRow>>(
            Expression.MemberInit(Expression.New(typeof(ScheduleRow)), bindings),
            row);
    }

    private static Expression<Func<ScheduleRow, ScheduleRow>> BuildTimeProjection(
        IReadOnlyList<CycleWindow> windows)
    {
        var row = Expression.Parameter(typeof(ScheduleRow), "row");
        var cycle = Expression.Property(row, nameof(ScheduleRow.EffectivePmCycle));
        var scheduleDate = Expression.Property(row, nameof(ScheduleRow.ScheduleDate));
        // Anchor the SQL CASE result to the datetimeoffset column; selected rows match known cycles.
        var start = BuildDateMap(cycle, windows, window => window.Start, scheduleDate);
        var deadline = BuildDateMap(cycle, windows, window => window.Deadline, scheduleDate);
        var bindings = CopyScheduleBindings(row,
            nameof(ScheduleRow.CycleStart),
            nameof(ScheduleRow.Deadline));
        bindings.Add(Expression.Bind(typeof(ScheduleRow).GetProperty(nameof(ScheduleRow.CycleStart))!, start));
        bindings.Add(Expression.Bind(typeof(ScheduleRow).GetProperty(nameof(ScheduleRow.Deadline))!, deadline));
        return Expression.Lambda<Func<ScheduleRow, ScheduleRow>>(
            Expression.MemberInit(Expression.New(typeof(ScheduleRow)), bindings),
            row);
    }

    private static Expression BuildDateMap(
        Expression cycle,
        IReadOnlyList<CycleWindow> windows,
        Func<CycleWindow, DateTimeOffset> select,
        Expression fallback)
    {
        Expression result = fallback;
        foreach (var window in windows.Reverse())
        {
            result = Expression.Condition(
                Expression.Equal(cycle, Expression.Constant(window.PmCycle)),
                Expression.Constant(select(window)),
                result);
        }

        return result;
    }

    private static List<MemberBinding> CopyScheduleBindings(
        Expression source,
        params string[] excluded)
    {
        var omitted = excluded.ToHashSet(StringComparer.Ordinal);
        return typeof(ScheduleRow)
            .GetProperties()
            .Where(property => !omitted.Contains(property.Name))
            .Select(property => (MemberBinding)Expression.Bind(
                property,
                Expression.Property(source, property.Name)))
            .ToList();
    }

    private static IQueryable<PmAnalyticsV2SqlRow> ApplyMatchingFilters(
        IQueryable<PmAnalyticsV2SqlRow> rows,
        PmAnalyticsV2Plan plan,
        DateTimeOffset now)
    {
        var matching = rows.Where(CreateMetricPredicate(plan.Metric, now));
        if (plan.Condition is { } condition)
        {
            matching = matching.Where(CreateConditionPredicate(condition));
        }

        if (plan.Timeliness is { } timeliness)
        {
            matching = matching.Where(CreateTimelinessPredicate(timeliness, now));
        }

        return matching;
    }

    private static Expression<Func<PmAnalyticsV2SqlRow, bool>> CreateMetricPredicate(
        PmAnalyticsV2Metric metric,
        DateTimeOffset now)
    {
        return metric switch
        {
            PmAnalyticsV2Metric.Progress => row => row.CompletedAt.HasValue,
            PmAnalyticsV2Metric.OnTimeCompliance => row => row.CompletedAt.HasValue
                && row.CompletedAt.Value <= row.Deadline,
            PmAnalyticsV2Metric.CompletedLate => row => row.CompletedAt.HasValue
                && row.CompletedAt.Value > row.Deadline,
            PmAnalyticsV2Metric.NonOperational => row => row.CompletedAt.HasValue
                && row.IsOperational == false,
            PmAnalyticsV2Metric.Remaining => row => !row.CompletedAt.HasValue
                && row.CycleStart <= now
                && row.Deadline >= now,
            PmAnalyticsV2Metric.Missed => row => !row.CompletedAt.HasValue
                && row.Deadline < now,
            _ => row => false
        };
    }

    private static Expression<Func<PmAnalyticsV2SqlRow, bool>> CreateConditionPredicate(
        PmAnalyticsV2Condition condition)
    {
        return condition switch
        {
            PmAnalyticsV2Condition.Operational => row => row.CompletedAt.HasValue
                && row.IsOperational == true,
            PmAnalyticsV2Condition.NonOperational => row => row.CompletedAt.HasValue
                && row.IsOperational == false,
            PmAnalyticsV2Condition.NotInspected => row => !row.CompletedAt.HasValue,
            _ => row => false
        };
    }

    private static Expression<Func<PmAnalyticsV2SqlRow, bool>> CreateTimelinessPredicate(
        PmAnalyticsV2Timeliness timeliness,
        DateTimeOffset now)
    {
        return timeliness switch
        {
            PmAnalyticsV2Timeliness.OnTime => row => row.CompletedAt.HasValue
                && row.CompletedAt.Value <= row.Deadline,
            PmAnalyticsV2Timeliness.Late => row => row.CompletedAt.HasValue
                && row.CompletedAt.Value > row.Deadline,
            PmAnalyticsV2Timeliness.Remaining => row => !row.CompletedAt.HasValue
                && row.CycleStart <= now
                && row.Deadline >= now,
            PmAnalyticsV2Timeliness.Missed => row => !row.CompletedAt.HasValue
                && row.Deadline < now,
            PmAnalyticsV2Timeliness.Future => row => !row.CompletedAt.HasValue
                && row.CycleStart > now,
            _ => row => false
        };
    }

    private static async Task<IReadOnlyList<CycleAggregate>> LoadCycleAggregatesAsync(
        IQueryable<PmAnalyticsV2SqlRow> rows,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await rows
            .GroupBy(row => row.PmCycle)
            .Select(group => new CycleAggregate
            {
                PmCycle = group.Key,
                AssetCycleCount = group.Count(),
                InspectedCount = group.Count(row => row.CompletedAt.HasValue),
                CompletedOnTimeCount = group.Count(row => row.CompletedAt.HasValue
                    && row.CompletedAt.Value <= row.Deadline),
                CompletedLateCount = group.Count(row => row.CompletedAt.HasValue
                    && row.CompletedAt.Value > row.Deadline),
                NonOperationalCount = group.Count(row => row.CompletedAt.HasValue
                    && row.IsOperational == false),
                RemainingCount = group.Count(row => !row.CompletedAt.HasValue
                    && row.CycleStart <= now
                    && row.Deadline >= now),
                MissedCount = group.Count(row => !row.CompletedAt.HasValue
                    && row.Deadline < now),
                LegacyDateFallbackCount = group.Count(row => row.UsedLegacyDateFallback),
                EligibleDistinctAssetCount = group.Select(row => row.AssetId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<MatchingCycleAggregate>> LoadMatchingCycleAggregatesAsync(
        IQueryable<PmAnalyticsV2SqlRow> matchingRows,
        CancellationToken cancellationToken)
    {
        return await matchingRows
            .GroupBy(row => row.PmCycle)
            .Select(group => new MatchingCycleAggregate
            {
                PmCycle = group.Key,
                AssetCycleCount = group.Count(),
                DistinctAssetCount = group.Select(row => row.AssetId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<GroupAggregate>> LoadGroupAggregatesAsync(
        IQueryable<PmAnalyticsV2SqlRow> rows,
        IQueryable<PmAnalyticsV2SqlRow> matchingRows,
        PmAnalyticsV2GroupBy groupBy,
        CancellationToken cancellationToken)
    {
        var selector = CreateGroupSelector(groupBy);
        var eligible = await rows
            .GroupBy(selector)
            .Select(group => new GroupAggregate
            {
                Value = group.Key,
                EligibleAssetCycleCount = group.Count(),
                EligibleDistinctAssetCount = group.Select(row => row.AssetId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        var matched = await matchingRows
            .GroupBy(selector)
            .Select(group => new MatchingGroupAggregate
            {
                Value = group.Key,
                MatchingAssetCycleCount = group.Count(),
                MatchingDistinctAssetCount = group.Select(row => row.AssetId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        return eligible
            .Select(group =>
            {
                var match = matched.FirstOrDefault(candidate =>
                    string.Equals(candidate.Value, group.Value, StringComparison.Ordinal));
                group.MatchingAssetCycleCount = match?.MatchingAssetCycleCount ?? 0;
                group.MatchingDistinctAssetCount = match?.MatchingDistinctAssetCount ?? 0;
                return group;
            })
            .OrderBy(group => group.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static Expression<Func<PmAnalyticsV2SqlRow, string?>> CreateGroupSelector(
        PmAnalyticsV2GroupBy groupBy)
    {
        return groupBy switch
        {
            PmAnalyticsV2GroupBy.Department => row => row.CurrentDepartment == null
                ? null
                : row.CurrentDepartment.Trim().ToUpper(),
            PmAnalyticsV2GroupBy.Building => row => row.CurrentBuilding == null
                ? null
                : row.CurrentBuilding.Trim().ToUpper(),
            PmAnalyticsV2GroupBy.PmCycle => row => row.PmCycle,
            _ => row => null
        };
    }

    private static async Task<int> CountDistinctAssetsAsync(
        IQueryable<PmAnalyticsV2SqlRow> rows,
        CancellationToken cancellationToken)
    {
        return await rows
            .Select(row => row.AssetId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static IReadOnlyList<PmAnalyticsV2CycleResponse> BuildCycleResponses(
        IReadOnlyList<CycleWindow> windows,
        IReadOnlyList<CycleAggregate> aggregates,
        DateTimeOffset now)
    {
        var lookup = aggregates.ToDictionary(aggregate => aggregate.PmCycle, StringComparer.Ordinal);
        return windows
            .Select(window =>
            {
                lookup.TryGetValue(window.PmCycle, out var aggregate);
                return new PmAnalyticsV2CycleResponse(
                    window.PmCycle,
                    window.Deadline,
                    ResolveCycleState(window, now),
                    aggregate?.AssetCycleCount ?? 0,
                    aggregate?.EligibleDistinctAssetCount ?? 0,
                    aggregate?.InspectedCount ?? 0,
                    aggregate?.CompletedOnTimeCount ?? 0,
                    aggregate?.CompletedLateCount ?? 0,
                    aggregate?.NonOperationalCount ?? 0,
                    aggregate?.RemainingCount ?? 0,
                    aggregate?.MissedCount ?? 0,
                    aggregate?.LegacyDateFallbackCount ?? 0);
            })
            .ToArray();
    }

    private static PmAnalyticsV2MeasureResponse BuildPeriodMeasure(
        PmAnalyticsV2Plan plan,
        int openCycleCount,
        int eligibleAssetCycles,
        int eligibleDistinctAssets,
        int matchingAssetCycles,
        int matchingDistinctAssets)
    {
        var numerator = plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
            ? matchingDistinctAssets
            : matchingAssetCycles;
        var denominator = plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
            ? eligibleDistinctAssets
            : eligibleAssetCycles;
        return CreateMeasure(plan, numerator, denominator, openCycleCount);
    }

    private static PmAnalyticsV2ComparisonResponse BuildComparison(
        PmAnalyticsV2Plan plan,
        IReadOnlyList<PmAnalyticsV2CycleResponse> cycles,
        IReadOnlyDictionary<string, MatchingCycleAggregate> matchingByCycle)
    {
        var earlierCycle = plan.Comparison!.EarlierCycle;
        var laterCycle = plan.Comparison.LaterCycle;
        var earlierSummary = cycles.Single(cycle => cycle.PmCycle == earlierCycle);
        var laterSummary = cycles.Single(cycle => cycle.PmCycle == laterCycle);
        matchingByCycle.TryGetValue(earlierCycle, out var earlierMatching);
        matchingByCycle.TryGetValue(laterCycle, out var laterMatching);
        var earlier = CreateMeasure(
            plan,
            plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                ? earlierMatching?.DistinctAssetCount ?? 0
                : earlierMatching?.AssetCycleCount ?? 0,
            plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                ? earlierSummary.EligibleDistinctAssetCount
                : earlierSummary.EligibleAssetCycleCount,
            earlierSummary.State == "Closed" ? 0 : 1);
        var later = CreateMeasure(
            plan,
            plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                ? laterMatching?.DistinctAssetCount ?? 0
                : laterMatching?.AssetCycleCount ?? 0,
            plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                ? laterSummary.EligibleDistinctAssetCount
                : laterSummary.EligibleAssetCycleCount,
            laterSummary.State == "Closed" ? 0 : 1);
        decimal? delta = null;
        if (earlier.Value is not null && later.Value is not null)
        {
            delta = plan.Presentation == PmAnalyticsV2Presentation.Percent
                ? Math.Round(
                    (decimal)later.Numerator * 100 / later.Denominator
                    - (decimal)earlier.Numerator * 100 / earlier.Denominator,
                    2,
                    MidpointRounding.AwayFromZero)
                : later.Value.Value - earlier.Value.Value;
        }
        return new PmAnalyticsV2ComparisonResponse(
            earlierCycle,
            laterCycle,
            earlier,
            later,
            delta,
            plan.Presentation == PmAnalyticsV2Presentation.Percent
                ? "PercentagePoints"
                : plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                    ? "DistinctAssets"
                    : "AssetCycles");
    }

    private static (PmAnalyticsV2GroupResponse[] Groups, PmAnalyticsV2RankingExclusionResponse[] Exclusions) BuildGroups(
        PmAnalyticsV2Plan plan,
        IReadOnlyList<GroupAggregate> aggregates,
        int openCycleCount,
        DateTimeOffset now,
        IReadOnlyDictionary<string, CycleWindow> cycleWindows)
    {
        var groups = aggregates
            .Select(aggregate =>
            {
                var groupOpenCycleCount = openCycleCount;
                if (plan.GroupBy == PmAnalyticsV2GroupBy.PmCycle
                    && aggregate.Value is { } cycle
                    && cycleWindows.TryGetValue(cycle, out var window))
                {
                    groupOpenCycleCount = window.Deadline >= now ? 1 : 0;
                }

                return new PmAnalyticsV2GroupResponse(
                    plan.GroupBy,
                    aggregate.Value,
                    CreateMeasure(
                        plan,
                        plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                            ? aggregate.MatchingDistinctAssetCount
                            : aggregate.MatchingAssetCycleCount,
                        plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                            ? aggregate.EligibleDistinctAssetCount
                            : aggregate.EligibleAssetCycleCount,
                        groupOpenCycleCount),
                    aggregate.EligibleAssetCycleCount,
                    aggregate.EligibleDistinctAssetCount,
                    aggregate.MatchingAssetCycleCount,
                    aggregate.MatchingDistinctAssetCount);
            })
            .ToArray();

        if (plan.Ranking is null)
        {
            return (groups, []);
        }

        var exclusions = new List<PmAnalyticsV2RankingExclusionResponse>();
        var eligible = new List<PmAnalyticsV2GroupResponse>();
        foreach (var group in groups)
        {
            if (group.Value is null)
            {
                exclusions.Add(new PmAnalyticsV2RankingExclusionResponse(null, "Department is not specified."));
                continue;
            }

            if (plan.Presentation == PmAnalyticsV2Presentation.Percent
                && openCycleCount > 0)
            {
                exclusions.Add(new PmAnalyticsV2RankingExclusionResponse(group.Value, "At least one relevant PM cycle is open."));
                continue;
            }

            if (group.Result.Value is null)
            {
                exclusions.Add(new PmAnalyticsV2RankingExclusionResponse(group.Value, "The selected group has no measurable denominator."));
                continue;
            }

            eligible.Add(group);
        }

        var ordered = plan.Ranking.Direction == PmAnalyticsV2Direction.Highest
            ? eligible.OrderByDescending(group => group.Result.Value)
                .ThenBy(group => group.Value, StringComparer.Ordinal)
            : eligible.OrderBy(group => group.Result.Value)
                .ThenBy(group => group.Value, StringComparer.Ordinal);
        return (ordered.Take(plan.Ranking.Limit).ToArray(), exclusions.ToArray());
    }

    private static PmAnalyticsV2MeasureResponse CreateMeasure(
        PmAnalyticsV2Plan plan,
        int numerator,
        int denominator,
        int openCycleCount)
    {
        var unit = plan.Presentation == PmAnalyticsV2Presentation.Percent
            ? "Percent"
            : plan.Presentation == PmAnalyticsV2Presentation.Records
                ? "Records"
                : plan.CountBasis == PmAnalyticsV2CountBasis.DistinctAssets
                    ? "DistinctAssets"
                    : "AssetCycles";
        if (denominator == 0)
        {
            return new PmAnalyticsV2MeasureResponse(
                numerator,
                denominator,
                null,
                unit,
                false,
                "No eligible asset-cycles are available in this scope.");
        }

        if (plan.Presentation == PmAnalyticsV2Presentation.Percent)
        {
            if (plan.Metric == PmAnalyticsV2Metric.OnTimeCompliance && openCycleCount > 0)
            {
                return new PmAnalyticsV2MeasureResponse(
                    numerator,
                    denominator,
                    null,
                    "Percent",
                    false,
                    "On-time compliance is unavailable while any relevant PM cycle is active or future.");
            }

            var percent = Math.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
            return new PmAnalyticsV2MeasureResponse(numerator, denominator, percent, "Percent", true, null);
        }

        return new PmAnalyticsV2MeasureResponse(numerator, denominator, numerator, unit, true, null);
    }

    private static PmAnalyticsV2SourceResponse ToSourceResponse(SourceProjection source, DateTimeOffset now)
    {
        var condition = !source.CompletedAt.HasValue
            ? "NotInspected"
            : source.IsOperational == true
                ? "Operational"
                : "NonOperational";
        var timeliness = !source.CompletedAt.HasValue
            ? now < source.CycleStart
                ? "Future"
                : now <= source.Deadline
                    ? "Remaining"
                    : "Missed"
            : source.CompletedAt.Value <= source.Deadline
                ? "OnTime"
                : "Late";
        return new PmAnalyticsV2SourceResponse(
            source.ScheduleId,
            source.AssetId,
            source.InspectionId,
            source.AssetCode,
            source.AssetCategory,
            source.CurrentDepartment,
            source.CurrentBuilding,
            source.PmCycle,
            source.Deadline,
            source.CompletedAt,
            source.ScheduleStatus,
            source.FormStatus,
            condition,
            timeliness,
            source.UsedLegacyDateFallback);
    }

    private static CycleWindow CreateCycleWindow(string cycle)
    {
        PreventiveMaintenanceCycle.TryParse(cycle, out var year, out var month);
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, PreventiveMaintenanceCycle.InstitutionalOffset);
        return new CycleWindow(
            cycle,
            start,
            start.AddMonths(1),
            PreventiveMaintenanceCycle.DeadlineForCycle(cycle));
    }

    private static string ResolveCycleState(CycleWindow window, DateTimeOffset now)
    {
        if (now < window.Start)
        {
            return "Future";
        }

        return now <= window.Deadline ? "Active" : "Closed";
    }

    private sealed record CycleWindow(
        string PmCycle,
        DateTimeOffset Start,
        DateTimeOffset NextStart,
        DateTimeOffset Deadline);

    private sealed class ScheduleRow
    {
        public Guid ScheduleId { get; set; }
        public Guid AssetId { get; set; }
        public string? StoredPmCycle { get; set; }
        public DateTimeOffset ScheduleDate { get; set; }
        public string ScheduleStatus { get; set; } = string.Empty;
        public string AssetCode { get; set; } = string.Empty;
        public string AssetCategory { get; set; } = string.Empty;
        public string? CurrentDepartment { get; set; }
        public string? CurrentBuilding { get; set; }
        public string? EffectivePmCycle { get; set; }
        public bool UsedLegacyDateFallback { get; set; }
        public DateTimeOffset CycleStart { get; set; }
        public DateTimeOffset Deadline { get; set; }
    }

    private sealed class PmAnalyticsV2SqlRow
    {
        public Guid ScheduleId { get; set; }
        public Guid AssetId { get; set; }
        public string AssetCode { get; set; } = string.Empty;
        public string AssetCategory { get; set; } = string.Empty;
        public string? CurrentDepartment { get; set; }
        public string? CurrentBuilding { get; set; }
        public string PmCycle { get; set; } = string.Empty;
        public string ScheduleStatus { get; set; } = string.Empty;
        public DateTimeOffset ScheduleDate { get; set; }
        public DateTimeOffset CycleStart { get; set; }
        public DateTimeOffset Deadline { get; set; }
        public bool UsedLegacyDateFallback { get; set; }
        public Guid? InspectionId { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public bool? IsOperational { get; set; }
        public string? FormStatus { get; set; }
    }

    private sealed record SourceProjection(
        Guid ScheduleId,
        Guid AssetId,
        Guid? InspectionId,
        string AssetCode,
        string AssetCategory,
        string? CurrentDepartment,
        string? CurrentBuilding,
        string PmCycle,
        DateTimeOffset CycleStart,
        DateTimeOffset Deadline,
        DateTimeOffset? CompletedAt,
        string ScheduleStatus,
        string? FormStatus,
        bool? IsOperational,
        bool UsedLegacyDateFallback);

    private sealed class CycleAggregate
    {
        public string PmCycle { get; set; } = string.Empty;
        public int AssetCycleCount { get; set; }
        public int EligibleDistinctAssetCount { get; set; }
        public int InspectedCount { get; set; }
        public int CompletedOnTimeCount { get; set; }
        public int CompletedLateCount { get; set; }
        public int NonOperationalCount { get; set; }
        public int RemainingCount { get; set; }
        public int MissedCount { get; set; }
        public int LegacyDateFallbackCount { get; set; }
    }

    private sealed class MatchingCycleAggregate
    {
        public string PmCycle { get; set; } = string.Empty;
        public int AssetCycleCount { get; set; }
        public int DistinctAssetCount { get; set; }
    }

    private sealed class GroupAggregate
    {
        public string? Value { get; set; }
        public int EligibleAssetCycleCount { get; set; }
        public int EligibleDistinctAssetCount { get; set; }
        public int MatchingAssetCycleCount { get; set; }
        public int MatchingDistinctAssetCount { get; set; }
    }

    private sealed class MatchingGroupAggregate
    {
        public string? Value { get; set; }
        public int MatchingAssetCycleCount { get; set; }
        public int MatchingDistinctAssetCount { get; set; }
    }
}
