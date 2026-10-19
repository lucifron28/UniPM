using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Reports;

internal sealed record PmAnalyticsV3PlanResolution(
    PmAnalyticsV3NormalizedPlan? Plan,
    Dictionary<string, string[]> Errors,
    IReadOnlyList<string> ClarificationFields);

internal sealed class PmAnalyticsV3ExecutionLimitException(string message) : Exception(message);

internal sealed class PmAnalyticsV3UnmeasurableThresholdException : Exception
{
    private const int MaximumReportedGroups = 20;

    internal PmAnalyticsV3UnmeasurableThresholdException(
        string measure,
        IReadOnlyList<string> groups)
        : base("An aggregate threshold could not be decided because its measure is unmeasurable for one or more groups.")
    {
        Measure = measure;
        TotalGroupCount = groups.Count;
        Groups = Array.AsReadOnly(groups.Take(MaximumReportedGroups).ToArray());
    }

    internal string Measure { get; }
    internal IReadOnlyList<string> Groups { get; }
    internal int TotalGroupCount { get; }
}

internal sealed class PmAnalyticsV3QueryService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
{
    internal const int MaximumPageSize = 100;
    internal const int MaximumPage = 100_000;
    internal const int MaximumRowsPerExecution = 50_000;
    private const int MaximumFilterCatalogMatches = 2;
    private const int MaximumCatalogValueResults = 50;

    internal static IReadOnlyList<string> ScopeNotes { get; } = Array.AsReadOnly(new[]
    {
        "The report uses eligible persisted schedules; cancelled schedules are excluded.",
        "Inspection completion uses CompletedAt, including Draft and Submitted forms; official history remains acknowledged-only.",
        "PM cycle boundaries and deadlines use Asia/Manila (UTC+08:00).",
        "Department, building and general location are current Asset fields, not historical snapshots.",
        "Blank stored PM cycles use ScheduleDate as an explicit fallback. Missing schedules are not inferred as missed.",
        "A non-operational count is a finding recorded on an inspection, not a current inventory-condition claim."
    });

    internal async Task<PmAnalyticsV3PlanResolution> NormalizeAndResolveAsync(
        PmAnalyticsV3ReportPlan? candidate,
        CancellationToken cancellationToken)
    {
        if (!PmAnalyticsV3PlanValidator.TryNormalize(candidate, out var normalized, out var errors))
        {
            return new PmAnalyticsV3PlanResolution(null, errors, []);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var clarificationFields = new HashSet<string>(StringComparer.Ordinal);
        var baseFilters = new List<PmAnalyticsV3Filter>();

        foreach (var filter in normalized.Plan.BaseFilters)
        {
            if (filter.Operator == PmAnalyticsV3FilterOperator.IsNull)
            {
                baseFilters.Add(filter);
                continue;
            }

            var values = new List<string>();
            foreach (var value in filter.Values ?? [])
            {
                if (filter.Field == PmAnalyticsV3Catalog.AssetId)
                {
                    if (!Guid.TryParse(value, out var assetId)
                        || !await context.Assets.AsNoTracking().AnyAsync(
                            asset => asset.Id == assetId
                                && normalized.Plan.Categories.Contains(asset.AssetCategory),
                            cancellationToken))
                    {
                        AddResolutionError(errors, clarificationFields, filter.Field, value, "Unknown");
                        continue;
                    }

                    values.Add(assetId.ToString("D", CultureInfo.InvariantCulture));
                    continue;
                }

                var selector = filter.Field switch
                {
                    PmAnalyticsV3Catalog.Department => (Expression<Func<Asset, string?>>)(asset => asset.Department),
                    PmAnalyticsV3Catalog.Building => asset => asset.Building,
                    PmAnalyticsV3Catalog.Location => asset => asset.Location,
                    PmAnalyticsV3Catalog.AssetCode => asset => asset.AssetCode,
                    _ => null
                };

                if (selector is null)
                {
                    AddError(errors, filter.Field, "The requested filter field cannot be resolved against the asset catalog.");
                    clarificationFields.Add(filter.Field);
                    continue;
                }

                var matches = await ResolveCatalogValueAsync(
                    context,
                    value,
                    selector,
                    normalized.Plan.Categories,
                    cancellationToken);
                if (matches.Count != 1)
                {
                    AddResolutionError(
                        errors,
                        clarificationFields,
                        filter.Field,
                        value,
                        matches.Count == 0 ? "Unknown" : "Ambiguous");
                    continue;
                }

                values.Add(ToUpperInvariant(matches[0]));
            }

            baseFilters.Add(filter with { Values = Array.AsReadOnly(values.ToArray()) });
        }

        if (errors.Count > 0)
        {
            return new PmAnalyticsV3PlanResolution(null, errors, clarificationFields.ToArray());
        }

        normalized = normalized with
        {
            Plan = normalized.Plan with { BaseFilters = Array.AsReadOnly(baseFilters.ToArray()) }
        };
        return new PmAnalyticsV3PlanResolution(normalized, errors, []);
    }

    internal static PmAnalyticsV3CatalogResponse CreateCatalogResponse() => new(
        3,
        PmAnalyticsV3Catalog.Version,
        PmAnalyticsV3Catalog.SubjectPmExecution,
        PmAnalyticsV3Catalog.Categories,
        PmAnalyticsV3Catalog.Measures,
        PmAnalyticsV3Catalog.Dimensions,
        PmAnalyticsV3Catalog.FilterFields,
        PmAnalyticsV3Catalog.RecordColumns);

    internal async Task<PmAnalyticsV3FilterValuesResponse> FindFilterValuesAsync(
        string field,
        IReadOnlyList<string> categories,
        string search,
        CancellationToken cancellationToken)
    {
        var selector = field switch
        {
            PmAnalyticsV3Catalog.Department => (Expression<Func<Asset, string?>>)(asset => asset.Department),
            PmAnalyticsV3Catalog.Building => asset => asset.Building,
            PmAnalyticsV3Catalog.Location => asset => asset.Location,
            PmAnalyticsV3Catalog.AssetCode => asset => asset.AssetCode,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        var searchKey = search.Trim().ToUpperInvariant();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var distinctValues = await context.Assets
            .AsNoTracking()
            .Where(asset => categories.Contains(asset.AssetCategory))
            .Select(selector)
            .Where(value => value != null && value.Trim().ToUpper().Contains(searchKey))
            .Select(value => value!.Trim())
            .GroupBy(value => value.ToUpper())
            .Select(group => group.Min())
            .OrderBy(value => value)
            .Take(MaximumCatalogValueResults + 1)
            .ToListAsync(cancellationToken);

        return new PmAnalyticsV3FilterValuesResponse(
            field,
            distinctValues.Take(MaximumCatalogValueResults).ToArray(),
            distinctValues.Count > MaximumCatalogValueResults);
    }

    internal async Task<PmAnalyticsV3ExecutionResponse> ExecuteAsync(
        PmAnalyticsV3NormalizedPlan normalized,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > MaximumPage || pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(page), $"Page must be between 1 and {MaximumPage}; PageSize must be between 1 and {MaximumPageSize}.");
        }

        var now = timeProvider.GetUtcNow();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var eligibleRows = await LoadEligibleRowsAsync(context, normalized, cancellationToken);
        var matchingRows = eligibleRows
            .Where(row => MatchesFilters(row, normalized.Plan.MatchingFilters, now))
            .ToArray();
        var columns = BuildColumns(normalized.Plan);

        if (normalized.Plan.Mode == PmAnalyticsV3Mode.Records)
        {
            return BuildRecordsResponse(normalized, eligibleRows, matchingRows, columns, now, page, pageSize);
        }

        return BuildAggregateResponse(normalized, eligibleRows, matchingRows, columns, now, page, pageSize);
    }

    internal static string CreatePreviewSummary(PmAnalyticsV3NormalizedPlan normalized)
    {
        var plan = normalized.Plan;
        var categoryScopes = normalized.CyclesByCategory.Select(entry =>
        {
            var label = PmAnalyticsV3Catalog.Categories.Single(item => item.Id == entry.Key).Label;
            return $"{label}: {DescribePeriod(entry.Value)}";
        });
        var scope = string.Join("; ", categoryScopes);
        var filters = plan.BaseFilters.Select(filter => "population: " + DescribeFilter(filter))
            .Concat(plan.MatchingFilters.Select(filter => "matching rows: " + DescribeFilter(filter)))
            .ToArray();

        var prefix = plan.Mode == PmAnalyticsV3Mode.Records
            ? $"Show PM execution records on scheduled-cycle time for {scope}"
            : $"Calculate {string.Join(", ", plan.Measures.Select(DescribeMeasure))} on scheduled-cycle time for {scope}";
        var groupBy = plan.GroupBy ?? [];
        var clauses = new List<string>
        {
            plan.Mode == PmAnalyticsV3Mode.Records
                ? "returned as matching records"
                : groupBy.Count == 0
                    ? "as one overall result"
                    : $"grouped by {string.Join(" and ", groupBy.Select(DescribeDimension))}"
        };
        clauses.AddRange(filters);
        clauses.AddRange(plan.AggregateFilters.Select(DescribeAggregateFilter));
        if (plan.Top is { } top)
        {
            clauses.Add($"{(top.Direction == PmAnalyticsV3SortDirection.Descending ? "top" : "bottom")} {top.Limit} by {DescribeMeasure(top.Measure)}");
        }

        if (plan.OrderBy.Count > 0)
        {
            clauses.Add("ordered by " + string.Join(", ", plan.OrderBy.Select(order =>
                $"{DescribeOutputColumn(order.Field)} {order.Direction.ToString().ToLowerInvariant()}")));
        }

        clauses.Add($"{plan.Presentation.ToString().ToLowerInvariant()} presentation");
        clauses.Add("columns: " + string.Join(", ", plan.Columns.Select(DescribeOutputColumn)));
        return $"{prefix}; {string.Join("; ", clauses)}.";
    }

    private static async Task<IReadOnlyList<string>> ResolveCatalogValueAsync(
        ApplicationDbContext context,
        string candidate,
        Expression<Func<Asset, string?>> selector,
        IReadOnlyList<string> categories,
        CancellationToken cancellationToken)
    {
        var normalizedCandidate = candidate.Trim().ToUpperInvariant();
        return await context.Assets
            .AsNoTracking()
            .Where(asset => categories.Contains(asset.AssetCategory))
            .Select(selector)
            .Where(value => value != null && value.Trim().ToUpper() == normalizedCandidate)
            .Select(value => value!.Trim().ToUpper())
            .Distinct()
            .Take(MaximumFilterCatalogMatches)
            .ToListAsync(cancellationToken);
    }

    private static async Task<PmAnalyticsV3Source[]> LoadEligibleRowsAsync(
        ApplicationDbContext context,
        PmAnalyticsV3NormalizedPlan normalized,
        CancellationToken cancellationToken)
    {
        var cycles = normalized.CyclesByCategory.Values
            .SelectMany(values => values)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var start = CycleStart(cycles[0]);
        var end = CycleStart(NextCycle(cycles[^1]));
        var baseCycles = normalized.Plan.BaseFilters
            .Where(filter => filter.Field != PmAnalyticsV3Catalog.AssetId)
            .ToArray();
        var assetIds = normalized.Plan.BaseFilters
            .Where(filter => filter.Field == PmAnalyticsV3Catalog.AssetId)
            .SelectMany(filter => filter.Values ?? [])
            .Select(value => Guid.Parse(value))
            .ToArray();

        var rows =
            from schedule in context.PreventiveMaintenanceSchedules.AsNoTracking()
            join asset in context.Assets.AsNoTracking() on schedule.AssetId equals asset.Id
            join inspection in context.InspectionRecords.AsNoTracking()
                on schedule.Id equals inspection.ScheduleId into inspections
            from inspection in inspections.DefaultIfEmpty()
            where schedule.Status != ScheduleStatusCatalog.Cancelled
                && normalized.Plan.Categories.Contains(asset.AssetCategory)
                && (schedule.PmCycle != null && schedule.PmCycle.Trim() != string.Empty
                    ? cycles.Contains(schedule.PmCycle.Trim())
                    : schedule.ScheduleDate >= start && schedule.ScheduleDate < end)
                && (assetIds.Length == 0 || assetIds.Contains(asset.Id))
            select new PmAnalyticsV3RawRow
            {
                ScheduleId = schedule.Id,
                AssetId = asset.Id,
                AssetCode = asset.AssetCode,
                AssetCategory = asset.AssetCategory,
                Department = asset.Department,
                Building = asset.Building,
                Location = asset.Location,
                StoredPmCycle = schedule.PmCycle,
                ScheduleDate = schedule.ScheduleDate,
                ScheduleStatus = schedule.Status,
                InspectionId = inspection == null ? null : (Guid?)inspection.Id,
                CompletedAt = inspection == null ? null : inspection.CompletedAt,
                IsOperational = inspection == null ? null : (bool?)inspection.IsOperational,
                FormStatus = inspection == null || inspection.PreventiveMaintenanceForm == null
                    ? null
                    : inspection.PreventiveMaintenanceForm.Status
            };

        foreach (var filter in baseCycles)
        {
            rows = ApplyBaseFilter(rows, filter);
        }

        var rawRows = await rows
            .Take(MaximumRowsPerExecution + 1)
            .ToListAsync(cancellationToken);
        if (rawRows.Count > MaximumRowsPerExecution)
        {
            throw new PmAnalyticsV3ExecutionLimitException(
                $"This report scope exceeds the current {MaximumRowsPerExecution:N0} eligible-row execution limit. Narrow the period or filters.");
        }

        var effectiveCycles = normalized.CyclesByCategory;
        var result = new List<PmAnalyticsV3Source>(rawRows.Count);
        foreach (var row in rawRows)
        {
            var storedCycle = row.StoredPmCycle?.Trim();
            var usedFallback = string.IsNullOrEmpty(storedCycle);
            var cycle = usedFallback
                ? PreventiveMaintenanceCycle.FromScheduleDate(row.ScheduleDate)
                : storedCycle!;
            if (!effectiveCycles.TryGetValue(row.AssetCategory, out var categoryCycles)
                || !categoryCycles.Contains(cycle, StringComparer.Ordinal))
            {
                continue;
            }

            result.Add(new PmAnalyticsV3Source(
                row.ScheduleId,
                row.AssetId,
                row.InspectionId,
                row.AssetCode,
                row.AssetCategory,
                NormalizeNullableText(row.Department),
                NormalizeNullableText(row.Building),
                NormalizeNullableText(row.Location),
                cycle,
                PreventiveMaintenanceCycle.DeadlineForCycle(cycle),
                row.CompletedAt,
                row.ScheduleStatus,
                row.FormStatus,
                row.IsOperational,
                usedFallback));
        }

        return result.ToArray();
    }

    private static IQueryable<PmAnalyticsV3RawRow> ApplyBaseFilter(
        IQueryable<PmAnalyticsV3RawRow> rows,
        PmAnalyticsV3Filter filter)
    {
        if (filter.Operator == PmAnalyticsV3FilterOperator.IsNull)
        {
            return filter.Field switch
            {
                PmAnalyticsV3Catalog.Department => rows.Where(row => row.Department == null || row.Department.Trim() == string.Empty),
                PmAnalyticsV3Catalog.Building => rows.Where(row => row.Building == null || row.Building.Trim() == string.Empty),
                PmAnalyticsV3Catalog.Location => rows.Where(row => row.Location == null || row.Location.Trim() == string.Empty),
                _ => rows
            };
        }

        var values = filter.Values ?? [];
        return filter.Field switch
        {
            PmAnalyticsV3Catalog.Department => rows.Where(row => row.Department != null
                && values.Contains(row.Department.Trim().ToUpper())),
            PmAnalyticsV3Catalog.Building => rows.Where(row => row.Building != null
                && values.Contains(row.Building.Trim().ToUpper())),
            PmAnalyticsV3Catalog.Location => rows.Where(row => row.Location != null
                && values.Contains(row.Location.Trim().ToUpper())),
            PmAnalyticsV3Catalog.AssetCode => rows.Where(row => values.Contains(row.AssetCode.Trim().ToUpper())),
            _ => rows
        };
    }

    private static bool MatchesFilters(
        PmAnalyticsV3Source row,
        IReadOnlyList<PmAnalyticsV3Filter> filters,
        DateTimeOffset now)
    {
        foreach (var filter in filters)
        {
            if (filter.Operator == PmAnalyticsV3FilterOperator.IsNull)
            {
                if (filter.Field != PmAnalyticsV3Catalog.FormStatus || row.FormStatus is not null)
                {
                    return false;
                }

                continue;
            }

            var value = filter.Field switch
            {
                PmAnalyticsV3Catalog.Condition => Condition(row),
                PmAnalyticsV3Catalog.Timeliness => Timeliness(row, now),
                PmAnalyticsV3Catalog.ScheduleStatus => row.ScheduleStatus,
                PmAnalyticsV3Catalog.FormStatus => row.FormStatus,
                _ => null
            };

            if (value is null || !(filter.Values ?? []).Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static PmAnalyticsV3ExecutionResponse BuildRecordsResponse(
        PmAnalyticsV3NormalizedPlan normalized,
        IReadOnlyList<PmAnalyticsV3Source> eligibleRows,
        IReadOnlyList<PmAnalyticsV3Source> matchingRows,
        IReadOnlyList<PmAnalyticsV3Column> columns,
        DateTimeOffset now,
        int page,
        int pageSize)
    {
        var ordered = matchingRows.ToList();
        if (normalized.Plan.OrderBy.Count == 0)
        {
            ordered.Sort(CompareStableSource);
        }
        else
        {
            ordered.Sort((left, right) => CompareSources(left, right, normalized.Plan.OrderBy, now));
        }

        var totalPages = PageCount(ordered.Count, pageSize);
        var skip = checked((page - 1) * pageSize);
        var records = ordered
            .Skip(skip)
            .Take(pageSize)
            .Select(row => new PmAnalyticsV3RecordRow(
                row.ScheduleId,
                row.AssetId,
                row.InspectionId,
                normalized.Plan.Columns.ToDictionary(
                    column => column,
                    column => RecordColumnValue(row, column, now),
                    StringComparer.Ordinal)))
            .ToArray();
        var population = new PmAnalyticsV3Population(
            eligibleRows.Count,
            eligibleRows.Select(row => row.AssetId).Distinct().Count(),
            matchingRows.Count,
            matchingRows.Select(row => row.AssetId).Distinct().Count(),
            matchingRows.Count,
            0,
            0,
            0);

        return new PmAnalyticsV3ExecutionResponse(
            3,
            PmAnalyticsV3Catalog.Version,
            now,
            normalized.Plan,
            normalized.CyclesByCategory,
            PmAnalyticsV3Mode.Records,
            columns,
            population,
            page,
            pageSize,
            totalPages,
            [],
            records,
            ScopeNotes);
    }

    private static PmAnalyticsV3ExecutionResponse BuildAggregateResponse(
        PmAnalyticsV3NormalizedPlan normalized,
        IReadOnlyList<PmAnalyticsV3Source> eligibleRows,
        IReadOnlyList<PmAnalyticsV3Source> matchingRows,
        IReadOnlyList<PmAnalyticsV3Column> columns,
        DateTimeOffset now,
        int page,
        int pageSize)
    {
        var groupBy = normalized.Plan.GroupBy ?? [];
        var eligibleByKey = BuildGroups(eligibleRows, groupBy);
        var matchingLookup = BuildGroups(matchingRows, groupBy)
            .ToDictionary(group => group.Key, group => group.Rows);
        var groups = new List<PmAnalyticsV3ComputedGroup>(eligibleByKey.Count);

        foreach (var group in eligibleByKey)
        {
            matchingLookup.TryGetValue(group.Key, out var matching);
            matching ??= [];
            var selectedCycles = CyclesForGroup(normalized, group.Key);
            var measures = new Dictionary<string, PmAnalyticsV3MeasureResult>(StringComparer.Ordinal);
            foreach (var measure in normalized.Plan.Measures)
            {
                measures[measure] = CalculateMeasure(
                    measure,
                    group.Rows,
                    matching,
                    now,
                    selectedCycles);
            }

            groups.Add(new PmAnalyticsV3ComputedGroup(group.Key, group.Rows, matching, measures));
        }

        if (groups.Count == 0 && groupBy.Count == 0)
        {
            var selectedCycles = CyclesForGroup(
                normalized,
                new PmAnalyticsV3GroupKey(null, null, null, null));
            var measures = normalized.Plan.Measures.ToDictionary(
                measure => measure,
                measure => CalculateMeasure(measure, [], [], now, selectedCycles),
                StringComparer.Ordinal);
            groups.Add(new PmAnalyticsV3ComputedGroup(new PmAnalyticsV3GroupKey(null, null, null, null), [], [], measures));
        }

        var beforeFilters = groups.Count;
        foreach (var filter in normalized.Plan.AggregateFilters)
        {
            var unmeasurable = groups
                .Where(group => !group.Measures[filter.Measure].IsMeasurable)
                .Select(group => DescribeGroupKey(group.Key))
                .ToArray();
            if (unmeasurable.Length > 0)
            {
                throw new PmAnalyticsV3UnmeasurableThresholdException(filter.Measure, unmeasurable);
            }

            groups = groups
                .Where(group => MatchesAggregateFilter(group.Measures[filter.Measure].Value!.Value, filter))
                .ToList();
        }

        if (normalized.Plan.Top is { } rankingTop)
        {
            var unmeasurable = groups
                .Where(group => !group.Measures[rankingTop.Measure].IsMeasurable)
                .Select(group => DescribeGroupKey(group.Key))
                .ToArray();
            if (unmeasurable.Length > 0)
            {
                throw new PmAnalyticsV3UnmeasurableThresholdException(rankingTop.Measure, unmeasurable);
            }
        }

        var afterFilters = groups.Count;
        SortGroups(groups, normalized.Plan);
        if (normalized.Plan.Top is { } top)
        {
            groups = top.Direction == PmAnalyticsV3SortDirection.Ascending
                ? groups.OrderBy(group => group.Measures[top.Measure].Value).ThenBy(group => DescribeGroupKey(group.Key), StringComparer.Ordinal).Take(top.Limit).ToList()
                : groups.OrderByDescending(group => group.Measures[top.Measure].Value).ThenBy(group => DescribeGroupKey(group.Key), StringComparer.Ordinal).Take(top.Limit).ToList();

            if (normalized.Plan.OrderBy.Count > 0)
            {
                SortGroups(groups, normalized.Plan);
            }
        }

        var returnedAfterTop = groups.Count;
        var totalPages = PageCount(groups.Count, pageSize);
        var skip = checked((page - 1) * pageSize);
        var outputRows = groups
            .Skip(skip)
            .Take(pageSize)
            .Select(group => new PmAnalyticsV3AggregateRow(
                normalized.Plan.Columns.ToDictionary(
                    column => column,
                    column => AggregateColumnValue(group.Key, group.Measures, column),
                    StringComparer.Ordinal),
                group.Measures))
            .ToArray();
        var population = new PmAnalyticsV3Population(
            eligibleRows.Count,
            eligibleRows.Select(row => row.AssetId).Distinct().Count(),
            matchingRows.Count,
            matchingRows.Select(row => row.AssetId).Distinct().Count(),
            matchingRows.Count,
            beforeFilters,
            afterFilters,
            returnedAfterTop);

        return new PmAnalyticsV3ExecutionResponse(
            3,
            PmAnalyticsV3Catalog.Version,
            now,
            normalized.Plan,
            normalized.CyclesByCategory,
            PmAnalyticsV3Mode.Aggregate,
            columns,
            population,
            page,
            pageSize,
            totalPages,
            outputRows,
            [],
            ScopeNotes);
    }

    private static List<PmAnalyticsV3Group> BuildGroups(
        IReadOnlyList<PmAnalyticsV3Source> rows,
        IReadOnlyList<string> groupBy)
    {
        if (groupBy.Count == 0)
        {
            return [new PmAnalyticsV3Group(new PmAnalyticsV3GroupKey(null, null, null, null), rows)];
        }

        return rows
            .GroupBy(row => CreateGroupKey(row, groupBy))
            .Select(group => new PmAnalyticsV3Group(group.Key, group.ToArray()))
            .ToList();
    }

    private static PmAnalyticsV3GroupKey CreateGroupKey(
        PmAnalyticsV3Source row,
        IReadOnlyList<string> groupBy)
    {
        var first = groupBy.Count > 0 ? DimensionValue(row, groupBy[0]) : null;
        var second = groupBy.Count > 1 ? DimensionValue(row, groupBy[1]) : null;
        return new PmAnalyticsV3GroupKey(
            groupBy.Count > 0 ? groupBy[0] : null,
            first,
            groupBy.Count > 1 ? groupBy[1] : null,
            second);
    }

    internal static PmAnalyticsV3MeasureResult CalculateMeasure(
        string measure,
        IReadOnlyList<PmAnalyticsV3Source> eligible,
        IReadOnlyList<PmAnalyticsV3Source> matching,
        DateTimeOffset now,
        IReadOnlyList<string> selectedCycles)
    {
        int numerator;
        int denominator;
        string unit;
        decimal? value;
        var measurable = true;
        string? reason = null;

        switch (measure)
        {
            case PmAnalyticsV3Catalog.ScheduledCount:
                numerator = eligible.Count;
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.CompletedCount:
                numerator = matching.Count(row => row.CompletedAt is not null);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.CompletedOnTimeCount:
                numerator = matching.Count(row => row.CompletedAt is { } completed && completed <= row.Deadline);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.CompletedLateCount:
                numerator = matching.Count(row => row.CompletedAt is { } completed && completed > row.Deadline);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.NonOperationalCount:
                numerator = matching.Count(row => row.CompletedAt is not null && row.IsOperational == false);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.RemainingCount:
                numerator = matching.Count(row => row.CompletedAt is null && row.CycleStart <= now && row.Deadline >= now);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.MissedCount:
                numerator = matching.Count(row => row.CompletedAt is null && row.Deadline < now);
                denominator = eligible.Count;
                unit = "asset-cycles";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.DistinctAssetCount:
                numerator = matching.Select(row => row.AssetId).Distinct().Count();
                denominator = eligible.Select(row => row.AssetId).Distinct().Count();
                unit = "assets";
                value = numerator;
                break;
            case PmAnalyticsV3Catalog.ProgressPercent:
                numerator = eligible.Count(row => row.CompletedAt is not null);
                denominator = eligible.Count;
                unit = "percent";
                value = denominator == 0 ? null : numerator * 100m / denominator;
                if (denominator == 0)
                {
                    measurable = false;
                    reason = "No eligible schedules are in this group.";
                }

                break;
            case PmAnalyticsV3Catalog.OnTimeCompliancePercent:
                numerator = eligible.Count(row => row.CompletedAt is { } completed && completed <= row.Deadline);
                denominator = eligible.Count;
                unit = "percent";
                var hasOpenCycle = selectedCycles.Any(cycle =>
                    PreventiveMaintenanceCycle.DeadlineForCycle(cycle) >= now);
                value = denominator == 0 || hasOpenCycle ? null : numerator * 100m / denominator;
                if (denominator == 0)
                {
                    measurable = false;
                    reason = "No eligible schedules are in this group.";
                }
                else if (hasOpenCycle)
                {
                    measurable = false;
                    reason = "At least one selected PM cycle is active or future.";
                }

                break;
            default:
                throw new InvalidOperationException($"Validated measure '{measure}' is not implemented.");
        }

        return new PmAnalyticsV3MeasureResult(
            value,
            numerator,
            denominator,
            unit,
            measurable,
            reason,
            eligible.Count,
            matching.Count);
    }

    private static IReadOnlyList<string> CyclesForGroup(
        PmAnalyticsV3NormalizedPlan normalized,
        PmAnalyticsV3GroupKey key)
    {
        var categoryGroup = new[]
            {
                (key.FirstId, key.FirstValue),
                (key.SecondId, key.SecondValue)
            }
            .FirstOrDefault(group => group.FirstId == PmAnalyticsV3Catalog.AssetCategory);
        var categories = categoryGroup.FirstId is null
            ? normalized.Plan.Categories
            : normalized.Plan.Categories
                .Where(category => string.Equals(category, categoryGroup.FirstValue as string, StringComparison.Ordinal))
                .ToArray();
        var cycles = categories
            .Where(normalized.CyclesByCategory.ContainsKey)
            .SelectMany(category => normalized.CyclesByCategory[category]);

        return cycles
            .Where(cycle => MatchesCycleDimension(cycle, key.FirstId, key.FirstValue)
                && MatchesCycleDimension(cycle, key.SecondId, key.SecondValue))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool MatchesCycleDimension(string cycle, string? dimension, object? value)
    {
        if (dimension is null || dimension is PmAnalyticsV3Catalog.AssetCategory
            or PmAnalyticsV3Catalog.Department or PmAnalyticsV3Catalog.Building)
        {
            return true;
        }

        return dimension switch
        {
            PmAnalyticsV3Catalog.PmCycle => string.Equals(cycle, value as string, StringComparison.Ordinal),
            PmAnalyticsV3Catalog.CalendarQuarter => string.Equals(
                $"{cycle[..4]}-Q{(int.Parse(cycle[5..], CultureInfo.InvariantCulture) - 1) / 3 + 1}",
                value as string,
                StringComparison.Ordinal),
            PmAnalyticsV3Catalog.CalendarYear => int.TryParse(cycle[..4], CultureInfo.InvariantCulture, out var year)
                && value is int selectedYear && year == selectedYear,
            _ => true
        };
    }

    private static void SortGroups(List<PmAnalyticsV3ComputedGroup> groups, PmAnalyticsV3ReportPlan plan)
    {
        if (plan.OrderBy.Count == 0)
        {
            groups.Sort((left, right) => string.CompareOrdinal(DescribeGroupKey(left.Key), DescribeGroupKey(right.Key)));
            return;
        }

        groups.Sort((left, right) =>
        {
            foreach (var order in plan.OrderBy)
            {
                var comparison = CompareAggregateValues(
                    AggregateColumnValue(left.Key, left.Measures, order.Field),
                    AggregateColumnValue(right.Key, right.Measures, order.Field));
                if (comparison != 0)
                {
                    return order.Direction == PmAnalyticsV3SortDirection.Ascending ? comparison : -comparison;
                }
            }

            return string.CompareOrdinal(DescribeGroupKey(left.Key), DescribeGroupKey(right.Key));
        });
    }

    private static bool MatchesAggregateFilter(decimal value, PmAnalyticsV3AggregateFilter filter) =>
        filter.Operator switch
        {
            PmAnalyticsV3NumericOperator.Equal => value == filter.Value,
            PmAnalyticsV3NumericOperator.NotEqual => value != filter.Value,
            PmAnalyticsV3NumericOperator.GreaterThan => value > filter.Value,
            PmAnalyticsV3NumericOperator.GreaterThanOrEqual => value >= filter.Value,
            PmAnalyticsV3NumericOperator.LessThan => value < filter.Value,
            PmAnalyticsV3NumericOperator.LessThanOrEqual => value <= filter.Value,
            _ => false
        };

    private static IReadOnlyList<PmAnalyticsV3Column> BuildColumns(PmAnalyticsV3ReportPlan plan)
    {
        return plan.Columns.Select(column =>
        {
            var measure = PmAnalyticsV3Catalog.Measures.FirstOrDefault(item => item.Id == column);
            if (measure is not null)
            {
                return new PmAnalyticsV3Column(measure.Id, measure.Label, measure.ValueType, measure.Unit, null);
            }

            var dimension = PmAnalyticsV3Catalog.Dimensions.FirstOrDefault(item => item.Id == column);
            if (dimension is not null)
            {
                return new PmAnalyticsV3Column(dimension.Id, dimension.Label, dimension.ValueType, "", dimension.NullSemantics);
            }

            var record = PmAnalyticsV3Catalog.FindRecordColumn(column)!;
            return new PmAnalyticsV3Column(record.Id, record.Label, record.ValueType, record.Unit, record.NullSemantics);
        }).ToArray();
    }

    private static object? RecordColumnValue(PmAnalyticsV3Source row, string column, DateTimeOffset now) =>
        column switch
        {
            "scheduleId" => row.ScheduleId,
            PmAnalyticsV3Catalog.AssetId => row.AssetId,
            PmAnalyticsV3Catalog.AssetCode => row.AssetCode,
            PmAnalyticsV3Catalog.AssetCategory => row.AssetCategory,
            PmAnalyticsV3Catalog.Department => row.Department,
            PmAnalyticsV3Catalog.Building => row.Building,
            PmAnalyticsV3Catalog.Location => row.Location,
            PmAnalyticsV3Catalog.PmCycle => row.PmCycle,
            PmAnalyticsV3Catalog.Deadline => row.Deadline,
            PmAnalyticsV3Catalog.CompletedAt => row.CompletedAt,
            PmAnalyticsV3Catalog.ScheduleStatus => row.ScheduleStatus,
            PmAnalyticsV3Catalog.FormStatus => row.FormStatus,
            PmAnalyticsV3Catalog.Condition => Condition(row),
            PmAnalyticsV3Catalog.Timeliness => Timeliness(row, now),
            PmAnalyticsV3Catalog.UsedLegacyDateFallback => row.UsedLegacyDateFallback,
            _ => null
        };

    private static object? AggregateColumnValue(
        PmAnalyticsV3GroupKey key,
        IReadOnlyDictionary<string, PmAnalyticsV3MeasureResult> measures,
        string column)
    {
        if (measures.TryGetValue(column, out var measure))
        {
            return measure.Value;
        }

        if (key.FirstId == column)
        {
            return key.FirstValue;
        }

        if (key.SecondId == column)
        {
            return key.SecondValue;
        }

        return null;
    }

    private static object? DimensionValue(PmAnalyticsV3Source row, string dimension) =>
        dimension switch
        {
            PmAnalyticsV3Catalog.AssetCategory => row.AssetCategory,
            PmAnalyticsV3Catalog.Department => row.Department?.ToUpperInvariant(),
            PmAnalyticsV3Catalog.Building => row.Building?.ToUpperInvariant(),
            PmAnalyticsV3Catalog.PmCycle => row.PmCycle,
            PmAnalyticsV3Catalog.CalendarQuarter => $"{row.PmCycle[..4]}-Q{(int.Parse(row.PmCycle[5..], CultureInfo.InvariantCulture) - 1) / 3 + 1}",
            PmAnalyticsV3Catalog.CalendarYear => int.Parse(row.PmCycle[..4], CultureInfo.InvariantCulture),
            _ => null
        };

    private static string Condition(PmAnalyticsV3Source row) => row.CompletedAt is null
        ? "NotInspected"
        : row.IsOperational == true ? "Operational" : "NonOperational";

    private static string Timeliness(PmAnalyticsV3Source row, DateTimeOffset now)
    {
        if (row.CompletedAt is { } completed)
        {
            return completed <= row.Deadline ? "OnTime" : "Late";
        }

        if (row.CycleStart > now)
        {
            return "Future";
        }

        return row.Deadline >= now ? "Remaining" : "Missed";
    }

    private static int CompareSources(
        PmAnalyticsV3Source left,
        PmAnalyticsV3Source right,
        IReadOnlyList<PmAnalyticsV3OrderBy> orderBy,
        DateTimeOffset now)
    {
        foreach (var order in orderBy)
        {
            var comparison = CompareAggregateValues(
                RecordColumnValue(left, order.Field, now),
                RecordColumnValue(right, order.Field, now));
            if (comparison != 0)
            {
                return order.Direction == PmAnalyticsV3SortDirection.Ascending ? comparison : -comparison;
            }
        }

        return CompareStableSource(left, right);
    }

    private static int CompareStableSource(PmAnalyticsV3Source left, PmAnalyticsV3Source right)
    {
        var compare = StringComparer.Ordinal.Compare(left.AssetCode, right.AssetCode);
        if (compare != 0) return compare;
        compare = StringComparer.Ordinal.Compare(left.PmCycle, right.PmCycle);
        return compare != 0 ? compare : left.ScheduleId.CompareTo(right.ScheduleId);
    }

    private static int CompareAggregateValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        return StringComparer.Ordinal.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture));
    }

    private static string DescribeGroupKey(PmAnalyticsV3GroupKey key) =>
        $"{key.FirstId ?? "overall"}={Convert.ToString(key.FirstValue, CultureInfo.InvariantCulture) ?? "(unknown)"}"
        + (key.SecondId is null
            ? string.Empty
            : $";{key.SecondId}={Convert.ToString(key.SecondValue, CultureInfo.InvariantCulture) ?? "(unknown)"}");

    private static string DescribeFilter(PmAnalyticsV3Filter filter)
    {
        var label = PmAnalyticsV3Catalog.FindFilterField(filter.Field)?.Label ?? filter.Field;
        return filter.Operator == PmAnalyticsV3FilterOperator.IsNull
            ? $"{label} is unknown"
            : $"{label} {string.Join(" or ", filter.Values ?? [])}";
    }

    private static string DescribeMeasure(string measure) =>
        PmAnalyticsV3Catalog.Measures.Single(item => item.Id == measure).Label;

    private static string DescribeOutputColumn(string column) =>
        PmAnalyticsV3Catalog.Measures.FirstOrDefault(item => item.Id == column)?.Label
        ?? PmAnalyticsV3Catalog.Dimensions.FirstOrDefault(item => item.Id == column)?.Label
        ?? PmAnalyticsV3Catalog.FindRecordColumn(column)?.Label
        ?? column;

    private static string DescribeAggregateFilter(PmAnalyticsV3AggregateFilter filter)
    {
        var operation = filter.Operator switch
        {
            PmAnalyticsV3NumericOperator.Equal => "=",
            PmAnalyticsV3NumericOperator.NotEqual => "≠",
            PmAnalyticsV3NumericOperator.GreaterThan => ">",
            PmAnalyticsV3NumericOperator.GreaterThanOrEqual => "≥",
            PmAnalyticsV3NumericOperator.LessThan => "<",
            PmAnalyticsV3NumericOperator.LessThanOrEqual => "≤",
            _ => filter.Operator.ToString()
        };
        var definition = PmAnalyticsV3Catalog.Measures.Single(item => item.Id == filter.Measure);
        return $"only groups where {definition.Label} {operation} {filter.Value.ToString(CultureInfo.InvariantCulture)}{(definition.Unit == "percent" ? "%" : string.Empty)}";
    }

    private static string DescribeDimension(string dimension) =>
        PmAnalyticsV3Catalog.Dimensions.FirstOrDefault(item => item.Id == dimension)?.Label ?? dimension;

    private static string DescribePeriod(IReadOnlyList<string> cycles)
    {
        var ordered = cycles.Order(StringComparer.Ordinal).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        return $"{ordered[0]} through {ordered[^1]} PM cycles";
    }

    private static DateTimeOffset CycleStart(string cycle)
    {
        PreventiveMaintenanceCycle.TryParse(cycle, out var year, out var month);
        return new DateTimeOffset(year, month, 1, 0, 0, 0, PreventiveMaintenanceCycle.InstitutionalOffset);
    }

    private static string NextCycle(string cycle)
    {
        PreventiveMaintenanceCycle.TryParse(cycle, out var year, out var month);
        var next = new DateTime(year, month, 1).AddMonths(1);
        return $"{next.Year:D4}-{next.Month:D2}";
    }

    private static string? NormalizeNullableText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int PageCount(int count, int pageSize) =>
        count == 0 ? 0 : (count + pageSize - 1) / pageSize;

    private static string ToUpperInvariant(string value) => value.Trim().ToUpperInvariant();

    private static void AddResolutionError(
        Dictionary<string, string[]> errors,
        ISet<string> clarificationFields,
        string field,
        string value,
        string reason)
    {
        AddError(errors, field, reason == "Unknown"
            ? $"Value '{value}' does not match the current authorized asset catalog."
            : $"Value '{value}' matches multiple current asset catalog values.");
        clarificationFields.Add(field);
    }

    private static void AddError(Dictionary<string, string[]> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var existing))
        {
            errors[field] = [message];
            return;
        }

        errors[field] = [.. existing, message];
    }

    private sealed class PmAnalyticsV3RawRow
    {
        public Guid ScheduleId { get; init; }
        public Guid AssetId { get; init; }
        public string AssetCode { get; init; } = string.Empty;
        public string AssetCategory { get; init; } = string.Empty;
        public string? Department { get; init; }
        public string? Building { get; init; }
        public string? Location { get; init; }
        public string StoredPmCycle { get; init; } = string.Empty;
        public DateTimeOffset ScheduleDate { get; init; }
        public string ScheduleStatus { get; init; } = string.Empty;
        public Guid? InspectionId { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
        public bool? IsOperational { get; init; }
        public string? FormStatus { get; init; }
    }

    internal sealed record PmAnalyticsV3Source(
        Guid ScheduleId,
        Guid AssetId,
        Guid? InspectionId,
        string AssetCode,
        string AssetCategory,
        string? Department,
        string? Building,
        string? Location,
        string PmCycle,
        DateTimeOffset Deadline,
        DateTimeOffset? CompletedAt,
        string ScheduleStatus,
        string? FormStatus,
        bool? IsOperational,
        bool UsedLegacyDateFallback)
    {
        internal DateTimeOffset CycleStart => PmAnalyticsV3QueryService.CycleStart(PmCycle);
    }

    private sealed record PmAnalyticsV3Group(PmAnalyticsV3GroupKey Key, IReadOnlyList<PmAnalyticsV3Source> Rows);
    private sealed record PmAnalyticsV3ComputedGroup(
        PmAnalyticsV3GroupKey Key,
        IReadOnlyList<PmAnalyticsV3Source> Rows,
        IReadOnlyList<PmAnalyticsV3Source> MatchingRows,
        IReadOnlyDictionary<string, PmAnalyticsV3MeasureResult> Measures);
    private sealed record PmAnalyticsV3GroupKey(string? FirstId, object? FirstValue, string? SecondId, object? SecondValue);
}
