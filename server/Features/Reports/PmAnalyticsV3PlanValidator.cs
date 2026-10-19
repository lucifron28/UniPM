using System.Globalization;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Reports;

internal sealed record PmAnalyticsV3NormalizedPlan(
    PmAnalyticsV3ReportPlan Plan,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CyclesByCategory);

/// <summary>
/// Validates the closed V3 plan vocabulary, clause compatibility and calendar
/// scope before catalog values are resolved or a query is constructed.
/// </summary>
internal static class PmAnalyticsV3PlanValidator
{
    private const int SchemaVersion = 3;
    private const int MaximumMeasures = 4;
    private const int MaximumGroupings = 2;
    private const int MaximumFilterFields = 8;
    private const int MaximumFilterValues = 20;
    private const int MaximumAggregateFilters = 4;
    private const int MaximumOrderFields = 2;
    private const int MaximumColumns = 20;
    private const int MaximumTop = 20;
    private const int MaximumPeriodMonths = 12;
    private const int MinimumYear = 2000;
    private const int MaximumYear = 9998;

    internal static bool TryNormalize(
        PmAnalyticsV3ReportPlan? candidate,
        out PmAnalyticsV3NormalizedPlan normalized,
        out Dictionary<string, string[]> errors)
    {
        normalized = null!;
        errors = [];

        if (candidate is null)
        {
            AddError(errors, "plan", "A version 3 report plan is required.");
            return false;
        }

        if (candidate.SchemaVersion != SchemaVersion)
        {
            AddError(errors, nameof(candidate.SchemaVersion), "SchemaVersion must be 3.");
        }

        if (candidate.CatalogVersion != PmAnalyticsV3Catalog.Version)
        {
            AddError(errors, nameof(candidate.CatalogVersion), $"CatalogVersion must be {PmAnalyticsV3Catalog.Version}.");
        }

        if (!string.Equals(candidate.Subject, PmAnalyticsV3Catalog.SubjectPmExecution, StringComparison.Ordinal))
        {
            AddError(errors, nameof(candidate.Subject), $"Subject must be '{PmAnalyticsV3Catalog.SubjectPmExecution}'.");
        }

        if (!Enum.IsDefined(candidate.Mode))
        {
            AddError(errors, nameof(candidate.Mode), "Mode is not supported.");
        }

        if (!Enum.IsDefined(candidate.Presentation))
        {
            AddError(errors, nameof(candidate.Presentation), "Presentation is not supported.");
        }

        var categories = NormalizeCategories(candidate.Categories, errors);
        var measures = NormalizeIdentifiers(
            candidate.Measures,
            nameof(candidate.Measures),
            PmAnalyticsV3Catalog.IsMeasure,
            "Measures must use identifiers from the approved V3 measure catalog.",
            MaximumMeasures,
            errors);
        var groupBy = NormalizeIdentifiers(
            candidate.GroupBy ?? [],
            nameof(candidate.GroupBy),
            PmAnalyticsV3Catalog.IsDimension,
            "GroupBy must use identifiers from the approved V3 dimension catalog.",
            MaximumGroupings,
            errors);
        var baseFilters = NormalizeFilters(candidate.BaseFilters, isBase: true, errors);
        var matchingFilters = NormalizeFilters(candidate.MatchingFilters, isBase: false, errors);
        var aggregateFilters = NormalizeAggregateFilters(candidate.AggregateFilters, measures, errors);
        var orderBy = NormalizeOrderBy(candidate.OrderBy, errors);
        var columns = NormalizeColumns(candidate.Columns, errors);

        if (candidate.Mode == PmAnalyticsV3Mode.Aggregate && measures.Count == 0)
        {
            AddError(errors, nameof(candidate.Measures), "Aggregate mode requires at least one measure.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Records && measures.Count > 0)
        {
            AddError(errors, nameof(candidate.Measures), "Records mode returns source fields; Measures must be empty.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Records && groupBy.Count > 0)
        {
            AddError(errors, nameof(candidate.GroupBy), "Records mode does not support grouping.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Records && aggregateFilters.Count > 0)
        {
            AddError(errors, nameof(candidate.AggregateFilters), "Records mode does not support aggregate filters.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Records && candidate.Top is not null)
        {
            AddError(errors, nameof(candidate.Top), "Records mode does not support top or bottom ranking.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Records
            && candidate.Presentation == PmAnalyticsV3Presentation.Summary)
        {
            AddError(errors, nameof(candidate.Presentation), "Records mode requires Table presentation.");
        }

        if (candidate.Presentation == PmAnalyticsV3Presentation.Summary
            && (candidate.Mode != PmAnalyticsV3Mode.Aggregate || groupBy.Count > 0))
        {
            AddError(errors, nameof(candidate.Presentation), "Summary presentation requires an ungrouped aggregate plan.");
        }

        if (candidate.Mode == PmAnalyticsV3Mode.Aggregate)
        {
            ValidateAggregateColumns(columns, measures, groupBy, errors);
            ValidateAggregateOrderBy(orderBy, columns, errors);
        }
        else if (candidate.Mode == PmAnalyticsV3Mode.Records)
        {
            ValidateRecordColumns(columns, errors);
            ValidateRecordOrderBy(orderBy, columns, errors);
        }

        if (candidate.Top is not null)
        {
            if (candidate.Mode != PmAnalyticsV3Mode.Aggregate || groupBy.Count == 0)
            {
                AddError(errors, nameof(candidate.Top), "Top or bottom selection requires a grouped aggregate plan.");
            }

            var topMeasure = candidate.Top.Measure?.Trim();
            if (string.IsNullOrEmpty(topMeasure)
                || !measures.Contains(topMeasure, StringComparer.Ordinal))
            {
                AddError(errors, "top.measure", "Top Measure must be one of the selected measures.");
            }

            if (candidate.Top.Limit is < 1 or > MaximumTop)
            {
                AddError(errors, "top.limit", $"Top Limit must be between 1 and {MaximumTop}.");
            }

            if (!Enum.IsDefined(candidate.Top.Direction))
            {
                AddError(errors, "top.direction", "Top Direction is not supported.");
            }

            if (!string.IsNullOrEmpty(topMeasure))
            {
                candidate = candidate with { Top = candidate.Top with { Measure = topMeasure } };
            }
        }

        if (matchingFilters.Count > 0
            && measures.Any(IsPercentageMeasure))
        {
            AddError(errors, nameof(candidate.MatchingFilters), "Outcome and workflow filters cannot be combined with percentage measures because they would change the denominator.");
        }

        IReadOnlyList<(int Year, int Month)> calendarMonths;
        string periodError;
        if (candidate.Period is null)
        {
            calendarMonths = [];
            periodError = "Period is required.";
            AddError(errors, nameof(candidate.Period), periodError);
        }
        else if (!TryExpandPeriod(candidate.Period, out calendarMonths, out periodError))
        {
            AddError(errors, nameof(candidate.Period), periodError);
            calendarMonths = [];
        }

        var cyclesByCategory = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (periodError.Length == 0)
        {
            foreach (var category in categories)
            {
                var cycles = calendarMonths
                    .Where(month => CpmpScheduleFrequency.IsValid(category, month.Month))
                    .Select(month => FormatCycle(month.Year, month.Month))
                    .ToArray();

                if (cycles.Length == 0)
                {
                    AddError(
                        errors,
                        nameof(candidate.Period),
                        $"The selected period contains no scheduled CPMP cycle for category '{category}'.");
                    continue;
                }

                cyclesByCategory.Add(category, Array.AsReadOnly(cycles));
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        var plan = candidate with
        {
            Subject = PmAnalyticsV3Catalog.SubjectPmExecution,
            Categories = Array.AsReadOnly(categories.ToArray()),
            Measures = Array.AsReadOnly(measures.ToArray()),
            GroupBy = Array.AsReadOnly(groupBy.ToArray()),
            BaseFilters = Array.AsReadOnly(baseFilters.ToArray()),
            MatchingFilters = Array.AsReadOnly(matchingFilters.ToArray()),
            AggregateFilters = Array.AsReadOnly(aggregateFilters.ToArray()),
            OrderBy = Array.AsReadOnly(orderBy.ToArray()),
            Columns = Array.AsReadOnly(columns.ToArray())
        };
        normalized = new PmAnalyticsV3NormalizedPlan(plan, cyclesByCategory);
        return true;
    }

    private static List<string> NormalizeCategories(
        IReadOnlyList<string>? values,
        Dictionary<string, string[]> errors)
    {
        var result = new List<string>();
        if (values is null || values.Count == 0)
        {
            AddError(errors, "Categories", "Select at least one supported asset category.");
            return result;
        }

        if (values.Count > PmAnalyticsV3Catalog.Categories.Count)
        {
            AddError(errors, "Categories", "Categories cannot contain more than the four supported CPMP categories.");
        }

        foreach (var value in values.Take(PmAnalyticsV3Catalog.Categories.Count))
        {
            if (string.IsNullOrWhiteSpace(value)
                || !AssetCategoryCatalog.TryNormalize(value, out var category)
                || !PmAnalyticsV3Catalog.IsCategory(category))
            {
                AddError(errors, "Categories", "Every category must resolve to one of the four supported CPMP category identifiers.");
                continue;
            }

            if (result.Contains(category, StringComparer.Ordinal))
            {
                AddError(errors, "Categories", $"Category '{category}' is duplicated.");
                continue;
            }

            result.Add(category);
        }

        return result;
    }

    private static List<string> NormalizeIdentifiers(
        IReadOnlyList<string>? values,
        string field,
        Func<string?, bool> isAllowed,
        string unsupportedMessage,
        int maximumCount,
        Dictionary<string, string[]> errors)
    {
        var result = new List<string>();
        if (values is null)
        {
            return result;
        }

        if (values.Count > maximumCount)
        {
            AddError(errors, field, $"{field} cannot contain more than {maximumCount} identifiers.");
        }

        foreach (var value in values.Take(maximumCount))
        {
            var identifier = value?.Trim();
            if (string.IsNullOrEmpty(identifier) || !isAllowed(identifier))
            {
                AddError(errors, field, unsupportedMessage);
                continue;
            }

            if (result.Contains(identifier, StringComparer.Ordinal))
            {
                AddError(errors, field, $"'{identifier}' is duplicated in {field}.");
                continue;
            }

            result.Add(identifier);
        }

        return result;
    }

    private static List<PmAnalyticsV3Filter> NormalizeFilters(
        IReadOnlyList<PmAnalyticsV3Filter>? filters,
        bool isBase,
        Dictionary<string, string[]> errors)
    {
        var fieldName = isBase ? "BaseFilters" : "MatchingFilters";
        var normalized = new List<PmAnalyticsV3Filter>();
        if (filters is null)
        {
            AddError(errors, fieldName, $"{fieldName} must be provided as an array.");
            return normalized;
        }

        if (filters.Count > MaximumFilterFields)
        {
            AddError(errors, fieldName, $"{fieldName} cannot contain more than {MaximumFilterFields} fields.");
        }

        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filter in filters.Take(MaximumFilterFields))
        {
            if (filter is null)
            {
                AddError(errors, fieldName, "Filter entries must be objects.");
                continue;
            }

            var fieldId = filter.Field?.Trim();
            var definition = PmAnalyticsV3Catalog.FindFilterField(fieldId);
            if (definition is null || definition.IsBaseFilter != isBase)
            {
                AddError(errors, fieldName, $"Field '{fieldId}' is not available in {fieldName}.");
                continue;
            }

            if (!seenFields.Add(definition.Id))
            {
                AddError(errors, fieldName, $"Field '{definition.Id}' can appear only once; use In for multiple values.");
                continue;
            }

            if (!Enum.IsDefined(filter.Operator) || !definition.Operators.Contains(filter.Operator))
            {
                AddError(errors, fieldName, $"Operator is not supported for field '{definition.Id}'.");
                continue;
            }

            var values = NormalizeFilterValues(filter, definition, fieldName, errors);
            normalized.Add(filter with
            {
                Field = definition.Id,
                Values = Array.AsReadOnly(values.ToArray())
            });
        }

        return normalized;
    }

    private static List<string> NormalizeFilterValues(
        PmAnalyticsV3Filter filter,
        PmAnalyticsV3FilterFieldDefinition definition,
        string fieldName,
        Dictionary<string, string[]> errors)
    {
        var values = filter.Values ?? [];
        if (filter.Operator == PmAnalyticsV3FilterOperator.IsNull)
        {
            if (values.Count != 0)
            {
                AddError(errors, fieldName, $"IsNull for '{definition.Id}' must not include Values.");
            }

            return [];
        }

        if (filter.Operator == PmAnalyticsV3FilterOperator.Equal && values.Count != 1)
        {
            AddError(errors, fieldName, $"Equal for '{definition.Id}' requires exactly one value.");
        }

        if (filter.Operator == PmAnalyticsV3FilterOperator.In
            && values.Count is < 2 or > MaximumFilterValues)
        {
            AddError(errors, fieldName, $"In for '{definition.Id}' requires between 2 and {MaximumFilterValues} values.");
        }

        var normalized = new List<string>();
        foreach (var rawValue in values.Take(MaximumFilterValues))
        {
            if (string.IsNullOrWhiteSpace(rawValue)
                || rawValue.Length > definition.MaximumLength
                || rawValue.Any(char.IsControl))
            {
                AddError(errors, fieldName, $"Values for '{definition.Id}' must be non-empty, at most {definition.MaximumLength} characters, and contain no control characters.");
                continue;
            }

            var value = NormalizeFilterValue(definition, rawValue.Trim());
            if (value is null)
            {
                AddError(errors, fieldName, $"Value '{rawValue}' is not valid for field '{definition.Id}'.");
                continue;
            }

            if (normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                AddError(errors, fieldName, $"Values for '{definition.Id}' must be unique.");
                continue;
            }

            normalized.Add(value);
        }

        return normalized;
    }

    private static string? NormalizeFilterValue(PmAnalyticsV3FilterFieldDefinition definition, string value)
    {
        return definition.ValueType switch
        {
            "guid" => Guid.TryParse(value, out var id) ? id.ToString("D", CultureInfo.InvariantCulture) : null,
            "condition" => definition.AllowedValues.FirstOrDefault(
                allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)),
            "timeliness" => definition.AllowedValues.FirstOrDefault(
                allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)),
            "scheduleStatus" or "formStatus" => definition.AllowedValues.FirstOrDefault(
                allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)),
            _ => value
        };
    }

    private static List<PmAnalyticsV3AggregateFilter> NormalizeAggregateFilters(
        IReadOnlyList<PmAnalyticsV3AggregateFilter>? filters,
        IReadOnlyList<string> measures,
        Dictionary<string, string[]> errors)
    {
        var result = new List<PmAnalyticsV3AggregateFilter>();
        if (filters is null)
        {
            AddError(errors, "AggregateFilters", "AggregateFilters must be provided as an array.");
            return result;
        }

        if (filters.Count > MaximumAggregateFilters)
        {
            AddError(errors, "AggregateFilters", $"AggregateFilters cannot contain more than {MaximumAggregateFilters} clauses.");
        }

        foreach (var filter in filters.Take(MaximumAggregateFilters))
        {
            if (filter is null)
            {
                AddError(errors, "AggregateFilters", "Aggregate filter entries must be objects.");
                continue;
            }

            var measure = filter.Measure?.Trim();
            if (string.IsNullOrEmpty(measure)
                || !PmAnalyticsV3Catalog.IsMeasure(measure)
                || !measures.Contains(measure, StringComparer.Ordinal))
            {
                AddError(errors, "AggregateFilters", "Each aggregate filter must reference a selected measure.");
                continue;
            }

            if (!Enum.IsDefined(filter.Operator))
            {
                AddError(errors, "AggregateFilters", "Aggregate filter operator is not supported.");
                continue;
            }

            var definition = PmAnalyticsV3Catalog.Measures.Single(item => item.Id == measure);
            if (filter.Value < 0
                || definition.Unit == "percent" && filter.Value > 100
                || definition.ValueType == "integer" && decimal.Truncate(filter.Value) != filter.Value)
            {
                AddError(errors, "AggregateFilters", $"Value for '{measure}' is outside its catalogued numeric range or precision.");
                continue;
            }

            result.Add(filter with { Measure = measure });
        }

        return result;
    }

    private static List<PmAnalyticsV3OrderBy> NormalizeOrderBy(
        IReadOnlyList<PmAnalyticsV3OrderBy>? orderBy,
        Dictionary<string, string[]> errors)
    {
        var result = new List<PmAnalyticsV3OrderBy>();
        if (orderBy is null)
        {
            AddError(errors, "OrderBy", "OrderBy must be provided as an array.");
            return result;
        }

        if (orderBy.Count > MaximumOrderFields)
        {
            AddError(errors, "OrderBy", $"OrderBy cannot contain more than {MaximumOrderFields} fields.");
        }

        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in orderBy.Take(MaximumOrderFields))
        {
            if (order is null)
            {
                AddError(errors, "OrderBy", "Order entries must be objects.");
                continue;
            }

            var field = order.Field?.Trim();
            if (string.IsNullOrEmpty(field))
            {
                AddError(errors, "OrderBy", "Every order field must be a catalog identifier.");
                continue;
            }

            if (!fields.Add(field))
            {
                AddError(errors, "OrderBy", $"Order field '{field}' is duplicated.");
                continue;
            }

            if (!Enum.IsDefined(order.Direction))
            {
                AddError(errors, "OrderBy", $"Direction for '{field}' is not supported.");
                continue;
            }

            result.Add(order with { Field = field });
        }

        return result;
    }

    private static List<string> NormalizeColumns(
        IReadOnlyList<string>? columns,
        Dictionary<string, string[]> errors)
    {
        var result = new List<string>();
        if (columns is null || columns.Count == 0)
        {
            AddError(errors, "Columns", "Select at least one approved output column.");
            return result;
        }

        if (columns.Count > MaximumColumns)
        {
            AddError(errors, "Columns", $"Columns cannot contain more than {MaximumColumns} fields.");
        }

        foreach (var column in columns.Take(MaximumColumns))
        {
            var id = column?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                AddError(errors, "Columns", "Every column must use a catalog identifier.");
                continue;
            }

            if (result.Contains(id, StringComparer.Ordinal))
            {
                AddError(errors, "Columns", $"Column '{id}' is duplicated.");
                continue;
            }

            result.Add(id);
        }

        return result;
    }

    private static void ValidateAggregateColumns(
        IReadOnlyList<string> columns,
        IReadOnlyList<string> measures,
        IReadOnlyList<string> groupBy,
        Dictionary<string, string[]> errors)
    {
        var selected = measures.Concat(groupBy).ToHashSet(StringComparer.Ordinal);
        foreach (var column in columns)
        {
            if (!selected.Contains(column))
            {
                AddError(errors, "Columns", $"Aggregate column '{column}' must be a selected measure or grouping dimension.");
            }
        }

        if (!measures.Any(measure => columns.Contains(measure, StringComparer.Ordinal)))
        {
            AddError(errors, "Columns", "Aggregate output must include at least one selected measure.");
        }

        foreach (var group in groupBy)
        {
            if (!columns.Contains(group, StringComparer.Ordinal))
            {
                AddError(errors, "Columns", $"Grouping dimension '{group}' must be included in output columns.");
            }
        }
    }

    private static void ValidateRecordColumns(
        IReadOnlyList<string> columns,
        Dictionary<string, string[]> errors)
    {
        foreach (var column in columns)
        {
            if (!PmAnalyticsV3Catalog.IsRecordColumn(column))
            {
                AddError(errors, "Columns", $"Record column '{column}' is not in the approved record-column catalog.");
            }
        }
    }

    private static void ValidateAggregateOrderBy(
        IReadOnlyList<PmAnalyticsV3OrderBy> orderBy,
        IReadOnlyList<string> columns,
        Dictionary<string, string[]> errors)
    {
        foreach (var item in orderBy)
        {
            if (!columns.Contains(item.Field, StringComparer.Ordinal))
            {
                AddError(errors, "OrderBy", $"Aggregate order field '{item.Field}' must be an output column.");
            }
        }
    }

    private static void ValidateRecordOrderBy(
        IReadOnlyList<PmAnalyticsV3OrderBy> orderBy,
        IReadOnlyList<string> columns,
        Dictionary<string, string[]> errors)
    {
        foreach (var item in orderBy)
        {
            if (!columns.Contains(item.Field, StringComparer.Ordinal)
                || !PmAnalyticsV3Catalog.IsRecordColumn(item.Field))
            {
                AddError(errors, "OrderBy", $"Record order field '{item.Field}' must be an output record column.");
            }
        }
    }

    private static bool IsPercentageMeasure(string measure) =>
        PmAnalyticsV3Catalog.Measures.Single(item => item.Id == measure).Unit == "percent";

    private static bool TryExpandPeriod(
        PmAnalyticsV3Period? period,
        out IReadOnlyList<(int Year, int Month)> months,
        out string error)
    {
        months = [];
        error = "Period is required.";

        if (period is null)
        {
            return false;
        }

        if (!string.Equals(period.Basis, "pmCycle", StringComparison.Ordinal))
        {
            error = "Period Basis must be 'pmCycle'.";
            return false;
        }

        int startYear;
        int startMonth;
        int endYear;
        int endMonth;

        switch (period.Kind)
        {
            case PmAnalyticsV3PeriodKind.Month:
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
                break;

            case PmAnalyticsV3PeriodKind.Quarter:
                if (!HasOnly(period, allowYear: true, allowQuarter: true)
                    || !TryValidateYear(period.Year, out startYear)
                    || period.Quarter is null
                    || period.Quarter.Value is < 1 or > 4)
                {
                    error = "Quarter periods require a supported Year and Quarter from 1 through 4 only.";
                    return false;
                }

                startMonth = (period.Quarter.Value - 1) * 3 + 1;
                endMonth = startMonth + 2;
                endYear = startYear;
                break;

            case PmAnalyticsV3PeriodKind.Year:
                if (!HasOnly(period, allowYear: true)
                    || !TryValidateYear(period.Year, out startYear))
                {
                    error = "Year periods require a supported Year and no other period fields.";
                    return false;
                }

                startMonth = 1;
                endMonth = 12;
                endYear = startYear;
                break;

            case PmAnalyticsV3PeriodKind.Range:
                if (!HasOnly(period, allowStartCycle: true, allowEndCycle: true)
                    || !TryParseCanonicalCycle(period.StartCycle, out startYear, out startMonth)
                    || !TryParseCanonicalCycle(period.EndCycle, out endYear, out endMonth))
                {
                    error = "Range periods require canonical StartCycle and EndCycle values in yyyy-MM format only.";
                    return false;
                }

                break;

            default:
                error = "Period Kind is not supported.";
                return false;
        }

        var startIndex = startYear * 12 + startMonth - 1;
        var endIndex = endYear * 12 + endMonth - 1;
        if (endIndex < startIndex)
        {
            error = "Period EndCycle must not be earlier than StartCycle.";
            return false;
        }

        var monthCount = endIndex - startIndex + 1;
        if (monthCount > MaximumPeriodMonths)
        {
            error = $"Period cannot exceed {MaximumPeriodMonths} calendar months.";
            return false;
        }

        var expanded = new (int Year, int Month)[monthCount];
        for (var index = 0; index < monthCount; index++)
        {
            var monthDate = new DateTime(startYear, startMonth, 1).AddMonths(index);
            expanded[index] = (monthDate.Year, monthDate.Month);
        }

        months = Array.AsReadOnly(expanded);
        error = string.Empty;
        return true;
    }

    private static bool HasOnly(
        PmAnalyticsV3Period period,
        bool allowYear = false,
        bool allowMonth = false,
        bool allowQuarter = false,
        bool allowStartCycle = false,
        bool allowEndCycle = false)
    {
        return (allowYear || period.Year is null)
            && (allowMonth || period.Month is null)
            && (allowQuarter || period.Quarter is null)
            && (allowStartCycle || period.StartCycle is null)
            && (allowEndCycle || period.EndCycle is null);
    }

    private static bool TryValidateYear(int? year, out int validatedYear)
    {
        validatedYear = year.GetValueOrDefault();
        return year is not null && validatedYear is >= MinimumYear and <= MaximumYear;
    }

    private static bool TryParseCanonicalCycle(string? value, out int year, out int month)
    {
        if (!PreventiveMaintenanceCycle.TryParse(value, out year, out month)
            || year is < MinimumYear or > MaximumYear)
        {
            return false;
        }

        return string.Equals(value, FormatCycle(year, month), StringComparison.Ordinal);
    }

    private static string FormatCycle(int year, int month) =>
        $"{year.ToString("D4", CultureInfo.InvariantCulture)}-{month.ToString("D2", CultureInfo.InvariantCulture)}";

    private static void AddError(Dictionary<string, string[]> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var existing))
        {
            errors[field] = [message];
            return;
        }

        errors[field] = [.. existing, message];
    }
}
