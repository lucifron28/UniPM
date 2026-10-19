using System.Globalization;
using System.Text.RegularExpressions;

namespace UniPM.Api.Features.Reports;

internal sealed class PmAnalyticsV2QuestionInterpreter(PmAnalyticsV2QueryService queryService)
{
    internal async Task<PmAnalyticsV2InterpretationResponse> InterpretAsync(
        string question,
        CancellationToken cancellationToken)
    {
        var parsed = PmAnalyticsV2QuestionGrammar.Parse(question);
        if (parsed.Plan is null)
        {
            return new PmAnalyticsV2InterpretationResponse(
                parsed.NeedsClarification ? "NeedsClarification" : "Unsupported",
                null,
                parsed.ClarificationFields,
                parsed.Code,
                parsed.Detail);
        }

        var resolution = await queryService.NormalizeAndResolveAsync(parsed.Plan, cancellationToken);
        if (resolution.Plan is not null)
        {
            return new PmAnalyticsV2InterpretationResponse("Valid", resolution.Plan.Plan, [], null, null);
        }

        return resolution.ClarificationFields.Count > 0
            ? new PmAnalyticsV2InterpretationResponse(
                "NeedsClarification",
                null,
                resolution.ClarificationFields,
                "UnknownContext",
                "The requested catalog context does not uniquely match current asset data.")
            : new PmAnalyticsV2InterpretationResponse(
                "Unsupported",
                null,
                [],
                "QuestionNotSupported",
                "The question does not match the supported analytics grammar.");
    }
}

internal sealed record PmAnalyticsV2QuestionParseResult(
    PmAnalyticsV2Plan? Plan,
    bool NeedsClarification,
    IReadOnlyList<string> ClarificationFields,
    string? Code,
    string? Detail);

internal sealed record PmAnalyticsV2ContextValues(string? Department, string? Building, string? AssetCode);

internal static class PmAnalyticsV2QuestionGrammar
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    // These words only connect already-recognized grammar. Outcome, ranking, and
    // negation words are deliberately absent so they cannot disappear silently.
    private static readonly HashSet<string> HarmlessWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "asset", "assets", "by", "for", "from", "give", "grouped",
        "all", "how", "in", "inspection", "inspections", "is", "list", "many",
        "me", "of", "on", "please",
        "pm", "record", "records", "report", "schedule", "schedules", "show", "the", "to", "what",
        "which", "with", "year", "during", "between", "compare", "versus", "vs", "count", "counts",
        "total", "overall",
        "department", "departments", "building", "buildings", "cycle", "cycles", "month", "quarter",
        "condition", "status", "that", "were", "was", "sources", "source",
        "evidence", "as", "their"
    };

    private static readonly (string Pattern, string Category)[] Categories =
    [
        (@"fire[- ]extinguishers?", "fire-extinguisher"),
        (@"fire[- ]alarms?(?:\s+systems?)?", "fire-alarm"),
        (@"emergency[- ]lights?", "emergency-light"),
        (@"water[- ]drinking[- ]stations?", "water-drinking-station")
    ];

    private static readonly (string Pattern, PmAnalyticsV2Metric Metric)[] Metrics =
    [
        (@"on[- ]time\s+compliance|compliance", PmAnalyticsV2Metric.OnTimeCompliance),
        (@"completed\s+late|late\s+inspections?", PmAnalyticsV2Metric.CompletedLate),
        (@"non[- ]operational\s+assets?|nonoperational\s+assets?", PmAnalyticsV2Metric.NonOperational),
        (@"remaining\s+(?:schedules?|asset[- ]cycles?)", PmAnalyticsV2Metric.Remaining),
        (@"missed\s+(?:(?:their|the)\s+)?(?:inspections?|schedules?|asset[- ]cycles?)", PmAnalyticsV2Metric.Missed),
        (@"progress", PmAnalyticsV2Metric.Progress)
    ];

    private static readonly Regex CyclePattern = CreateRegex(@"\b(?<year>20\d{2})-(?<month>0[1-9]|1[0-2])\b");
    private static readonly Regex QuarterPattern = CreateRegex(
        @"\b(?:(?:q|quarter\s*)(?<quarter>[1-4])|(?<word>first|second|third|fourth|1st|2nd|3rd|4th)\s+quarter)(?:\s+of)?\s+(?<year>20\d{2})\b");
    private static readonly Regex QuarterMarkerPattern = CreateRegex(
        @"\b(?:q[1-4]|(?:first|second|third|fourth|1st|2nd|3rd|4th)\s+quarter|quarter(?:\s+[1-4])?)\b");
    private static readonly Regex MonthPattern = CreateRegex(
        @"\b(?<month>january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|sept|october|oct|november|nov|december|dec)(?:\s+of)?\s+(?<year>20\d{2})\b");
    private static readonly Regex MonthTokenPattern = CreateRegex(
        @"\b(?:january|jan|february|feb|march|mar|april|apr|may|june|jun|july|jul|august|aug|september|sep|sept|october|oct|november|nov|december|dec)\b");
    private static readonly Regex MonthMarkerPattern = CreateRegex(@"\bmonth\b");
    private static readonly Regex ContextPattern = CreateRegex(
        @"\b(?<field>department|building|asset(?:\s+code)?)\s+""(?<value>[^""]+)""");
    private static readonly Regex RankingPattern = CreateRegex(
        @"\b(?<direction>top|bottom|highest|lowest)\s+(?<limit>\d{1,2})\s+departments?\b");
    private static readonly Regex TokenPattern = CreateRegex(@"[\p{L}\p{N}]+(?:[-'][\p{L}\p{N}]+)*");

    internal static PmAnalyticsV2QuestionParseResult Parse(string question)
    {
        if (string.IsNullOrWhiteSpace(question)
            || question.Length > 512
            || question.Any(char.IsControl))
        {
            return Unsupported("InvalidQuestion", "Question must be non-empty and at most 512 characters.");
        }

        var residual = question.Trim().Replace('’', '\'').Replace('“', '"').Replace('”', '"');
        var contexts = ParseContexts(ref residual);
        if (contexts is null)
        {
            return Unsupported("QuestionNotSupported", "Context values must use one quoted catalog value per field.");
        }

        var countBasis = ParseCountBasis(ref residual);
        var categoryMatches = Categories
            .SelectMany(item => FindMatches(residual, item.Pattern)
                .Select(match => (item.Category, Match: match)));
        var selectedCategories = SelectLongestNonOverlapping(categoryMatches);
        var categories = selectedCategories
            .Select(item => item.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (categories.Length > 1)
        {
            return Unsupported("QuestionNotSupported", "One asset category is supported per analytics question.");
        }

        RemoveMatches(ref residual, selectedCategories.Select(item => item.Match));

        var periodResult = ParsePeriod(ref residual);
        if (periodResult.Unsupported)
        {
            return Unsupported("UnsupportedPeriod", "The requested period or comparison is outside the supported CPMP cycle grammar.");
        }

        var metricMatches = Metrics
            .SelectMany(item => FindMatches(residual, item.Pattern)
                .Select(match => (item.Metric, Match: match)));
        var selectedMetrics = SelectLongestNonOverlapping(metricMatches);
        var metricValues = selectedMetrics.Select(item => item.Value).Distinct().ToArray();
        if (metricValues.Length > 1)
        {
            return Unsupported("QuestionNotSupported", "One analytics metric is supported per question.");
        }

        PmAnalyticsV2Metric? metric = metricValues.Length == 1 ? metricValues[0] : null;
        RemoveMatches(ref residual, selectedMetrics.Select(item => item.Match));

        var condition = ParseSingleFilter(
            ref residual,
            [
                (@"not\s+inspected|uninspected", PmAnalyticsV2Condition.NotInspected),
                (@"non[- ]operational", PmAnalyticsV2Condition.NonOperational),
                (@"operational", PmAnalyticsV2Condition.Operational)
            ]);
        if (condition.Ambiguous)
        {
            return Unsupported("QuestionNotSupported", "One inspection condition filter is supported per question.");
        }

        var timeliness = ParseSingleFilter(
            ref residual,
            [
                (@"on[- ]time", PmAnalyticsV2Timeliness.OnTime),
                (@"late", PmAnalyticsV2Timeliness.Late),
                (@"remaining", PmAnalyticsV2Timeliness.Remaining),
                (@"missed", PmAnalyticsV2Timeliness.Missed),
                (@"future", PmAnalyticsV2Timeliness.Future)
            ]);
        if (timeliness.Ambiguous)
        {
            return Unsupported("QuestionNotSupported", "One timeliness filter is supported per question.");
        }

        if (condition.Value == PmAnalyticsV2Condition.NonOperational)
        {
            RemoveLiteral(ref residual, @"\brecorded\s+as\b");
        }

        var groupBy = ParseGroupBy(ref residual, out var groupAmbiguous);
        if (groupAmbiguous)
        {
            return Unsupported("QuestionNotSupported", "One grouping field is supported per question.");
        }

        var ranking = ParseRanking(ref residual, out var rankingInvalid);
        if (rankingInvalid)
        {
            return Unsupported("QuestionNotSupported", "Department ranking must use a limit from 1 through 10.");
        }

        var (presentation, presentationAmbiguous) = ParsePresentation(ref residual);
        if (presentationAmbiguous)
        {
            return Unsupported("QuestionNotSupported", "Choose one presentation: count, percent, or records.");
        }

        var completionMentions = FindMatches(residual, @"completed|inspected");
        if (completionMentions.Count > 0
            && (condition.Value == PmAnalyticsV2Condition.NotInspected
                || metric is PmAnalyticsV2Metric.Remaining or PmAnalyticsV2Metric.Missed
                || timeliness.Value is PmAnalyticsV2Timeliness.Remaining
                    or PmAnalyticsV2Timeliness.Missed
                    or PmAnalyticsV2Timeliness.Future))
        {
            return Unsupported("QuestionNotSupported", "Completed and uninspected schedule conditions cannot be requested together.");
        }

        if (metric is null)
        {
            if (timeliness.Value == PmAnalyticsV2Timeliness.Late)
            {
                metric = PmAnalyticsV2Metric.CompletedLate;
            }
            else if (condition.Value == PmAnalyticsV2Condition.NonOperational)
            {
                metric = PmAnalyticsV2Metric.NonOperational;
            }
            else if (timeliness.Value == PmAnalyticsV2Timeliness.Missed)
            {
                metric = PmAnalyticsV2Metric.Missed;
            }
            else if (timeliness.Value == PmAnalyticsV2Timeliness.Remaining)
            {
                metric = PmAnalyticsV2Metric.Remaining;
            }
            else if (completionMentions.Count > 0)
            {
                metric = PmAnalyticsV2Metric.Progress;
            }
            else if ((condition.Value == PmAnalyticsV2Condition.Operational
                    || timeliness.Value == PmAnalyticsV2Timeliness.OnTime)
                && Regex.IsMatch(residual, @"\b(?:inspections?|schedules?)\b", Options, RegexTimeout))
            {
                metric = PmAnalyticsV2Metric.Progress;
            }
        }

        if (completionMentions.Count > 0)
        {
            if (metric is PmAnalyticsV2Metric.Progress
                or PmAnalyticsV2Metric.OnTimeCompliance
                or PmAnalyticsV2Metric.CompletedLate
                or PmAnalyticsV2Metric.NonOperational)
            {
                RemoveMatches(ref residual, completionMentions);
            }
            else
            {
                return Unsupported("QuestionNotSupported", "The completion wording conflicts with the selected analytics metric.");
            }
        }

        var remainingTokens = TokenPattern.Matches(residual)
            .Select(match => match.Value)
            .Where(token => !HarmlessWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (remainingTokens.Length > 0)
        {
            return Unsupported("QuestionNotSupported", "The question contains scope or semantics that are not supported by the deterministic grammar.");
        }

        var clarificationFields = new List<string>();
        if (metric is null)
        {
            clarificationFields.Add("Metric");
        }

        if (categories.Length == 0)
        {
            clarificationFields.Add("AssetCategory");
        }

        if (periodResult.ClarificationFields.Count > 0)
        {
            clarificationFields.AddRange(periodResult.ClarificationFields);
        }
        else if (periodResult.Period is null && periodResult.Comparison is null)
        {
            clarificationFields.Add("Period");
        }

        if (clarificationFields.Count > 0)
        {
            return new PmAnalyticsV2QuestionParseResult(
                null,
                true,
                clarificationFields.Distinct(StringComparer.Ordinal).ToArray(),
                null,
                "Specify an unambiguous metric, one CPMP asset category, and a bounded PM period.");
        }

        if (ranking is not null && groupBy == PmAnalyticsV2GroupBy.None)
        {
            groupBy = PmAnalyticsV2GroupBy.Department;
        }

        var selectedMetric = metric!.Value;
        presentation ??= selectedMetric == PmAnalyticsV2Metric.OnTimeCompliance
            || (selectedMetric == PmAnalyticsV2Metric.Progress
                && condition.Value is null
                && timeliness.Value is null)
                ? PmAnalyticsV2Presentation.Percent
                : PmAnalyticsV2Presentation.Count;
        var plan = new PmAnalyticsV2Plan(
            2,
            selectedMetric,
            categories[0],
            periodResult.Period,
            periodResult.Comparison,
            presentation.Value,
            countBasis,
            contexts.Department,
            contexts.Building,
            contexts.AssetCode,
            condition.Value,
            timeliness.Value,
            groupBy,
            ranking);
        return new PmAnalyticsV2QuestionParseResult(plan, false, [], null, null);
    }

    private static PmAnalyticsV2ContextValues? ParseContexts(ref string residual)
    {
        string? department = null;
        string? building = null;
        string? assetCode = null;
        var matches = ContextPattern.Matches(residual).Cast<Match>().ToArray();
        foreach (var match in matches)
        {
            var field = match.Groups["field"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value.Trim();
            if (value.Length == 0)
            {
                return null;
            }

            switch (field)
            {
                case "department" when department is null:
                    department = value;
                    break;
                case "building" when building is null:
                    building = value;
                    break;
                case "asset" when assetCode is null:
                case "asset code" when assetCode is null:
                    assetCode = value;
                    break;
                default:
                    return null;
            }
        }

        RemoveMatches(ref residual, matches);
        return new PmAnalyticsV2ContextValues(department, building, assetCode);
    }

    private static PmAnalyticsV2PeriodParse ParsePeriod(ref string residual)
    {
        var comparisonWordPresent = Regex.IsMatch(residual, @"\b(?:compare|versus|vs\.?)\b", Options, RegexTimeout);
        var departmentGroupingRequested = Regex.IsMatch(
            residual,
            @"\b(?:grouped\s+by|by|across)\s+departments?\b",
            Options,
            RegexTimeout);
        var comparisonRequested = comparisonWordPresent && !departmentGroupingRequested;
        var rangeMatch = CreateRegex(
            @"\bfrom\s+(?<start>20\d{2}-(?:0[1-9]|1[0-2]))\s+to\s+(?<end>20\d{2}-(?:0[1-9]|1[0-2]))\b")
            .Match(residual);
        if (rangeMatch.Success)
        {
            if (comparisonRequested)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            RemoveMatch(ref residual, rangeMatch);
            return new PmAnalyticsV2PeriodParse(
                new PmAnalyticsV2Period(
                    PmAnalyticsV2PeriodKind.Range,
                    StartCycle: rangeMatch.Groups["start"].Value,
                    EndCycle: rangeMatch.Groups["end"].Value),
                null,
                false,
                []);
        }

        var quarterMatches = QuarterPattern.Matches(residual).Cast<Match>().ToArray();
        var fullDates = new List<(string Cycle, Match Match)>();
        fullDates.AddRange(CyclePattern.Matches(residual).Cast<Match>()
            .Select(match => (FormatCycle(match), match)));
        fullDates.AddRange(MonthPattern.Matches(residual).Cast<Match>()
            .Select(match => (FormatMonthCycle(match), match)));
        var selectedDates = SelectLongestNonOverlapping(fullDates);

        if (comparisonRequested)
        {
            if (quarterMatches.Length > 0)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            if (selectedDates.Count == 2
                && !string.Equals(selectedDates[0].Value, selectedDates[1].Value, StringComparison.Ordinal))
            {
                RemoveMatches(ref residual, selectedDates.Select(item => item.Match));
                var orderedCycles = selectedDates
                    .Select(item => item.Value)
                    .OrderBy(cycle => cycle, StringComparer.Ordinal)
                    .ToArray();
                return new PmAnalyticsV2PeriodParse(
                    null,
                    new PmAnalyticsV2Comparison(orderedCycles[0], orderedCycles[1]),
                    false,
                    []);
            }

            var monthTokens = MonthTokenPattern.Matches(residual).Cast<Match>().ToArray();
            if (selectedDates.Count == 0 && monthTokens.Length == 2)
            {
                RemoveMatches(ref residual, monthTokens);
                RemoveLooseYears(ref residual, out var yearCount);
                return yearCount > 1
                    ? PmAnalyticsV2PeriodParse.UnsupportedResult
                    : new PmAnalyticsV2PeriodParse(null, null, false, ["Year"]);
            }

            if (selectedDates.Count == 1)
            {
                RemoveMatch(ref residual, selectedDates[0].Match);
                return monthTokens.Length > 1
                    ? new PmAnalyticsV2PeriodParse(null, null, false, ["Year", "ComparisonCycle"])
                    : new PmAnalyticsV2PeriodParse(null, null, false, ["ComparisonCycle"]);
            }

            return PmAnalyticsV2PeriodParse.UnsupportedResult;
        }

        if (quarterMatches.Length > 1 || (quarterMatches.Length == 1 && selectedDates.Count > 0))
        {
            return PmAnalyticsV2PeriodParse.UnsupportedResult;
        }

        if (quarterMatches.Length == 1)
        {
            var quarterMatch = quarterMatches[0];
            var quarter = quarterMatch.Groups["quarter"].Success
                ? int.Parse(quarterMatch.Groups["quarter"].Value, CultureInfo.InvariantCulture)
                : quarterMatch.Groups["word"].Value.ToLowerInvariant() switch
                {
                    "first" or "1st" => 1,
                    "second" or "2nd" => 2,
                    "third" or "3rd" => 3,
                    _ => 4
                };
            var year = int.Parse(quarterMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
            RemoveMatch(ref residual, quarterMatch);
            RemoveLooseYears(ref residual, out var extraYears);
            return extraYears == 0
                ? new PmAnalyticsV2PeriodParse(
                    new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Quarter, Year: year, Quarter: quarter),
                    null,
                    false,
                    [])
                : PmAnalyticsV2PeriodParse.UnsupportedResult;
        }

        if (selectedDates.Count == 1)
        {
            var selected = selectedDates[0];
            RemoveMatch(ref residual, selected.Match);
            RemoveLooseYears(ref residual, out var extraYears);
            if (extraYears != 0)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            var parts = selected.Value.Split('-', StringSplitOptions.None);
            return new PmAnalyticsV2PeriodParse(
                new PmAnalyticsV2Period(
                    PmAnalyticsV2PeriodKind.Month,
                    Year: int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Month: int.Parse(parts[1], CultureInfo.InvariantCulture)),
                null,
                false,
                []);
        }

        if (selectedDates.Count > 1)
        {
            return PmAnalyticsV2PeriodParse.UnsupportedResult;
        }

        var monthTokensWithoutYear = MonthTokenPattern.Matches(residual).Cast<Match>().ToArray();
        var partialQuarters = QuarterMarkerPattern.Matches(residual).Cast<Match>().ToArray();
        var looseYears = Regex.Matches(residual, @"\b20\d{2}\b", Options, RegexTimeout).Cast<Match>().ToArray();
        var annualTokens = FindMatches(residual, @"annual|yearly");

        if (monthTokensWithoutYear.Length > 0)
        {
            if (monthTokensWithoutYear.Length != 1 || partialQuarters.Length > 0 || looseYears.Length > 1)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            RemoveMatches(ref residual, monthTokensWithoutYear);
            RemoveMatches(ref residual, looseYears);
            return new PmAnalyticsV2PeriodParse(null, null, false, ["Year"]);
        }

        var genericMonthMarkers = MonthMarkerPattern.Matches(residual).Cast<Match>().ToArray();
        if (genericMonthMarkers.Length > 0)
        {
            if (genericMonthMarkers.Length > 1 || partialQuarters.Length > 0 || looseYears.Length > 1)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            RemoveMatches(ref residual, genericMonthMarkers);
            RemoveMatches(ref residual, looseYears);
            return new PmAnalyticsV2PeriodParse(
                null,
                null,
                false,
                looseYears.Length == 1 ? ["Month"] : ["Month", "Year"]);
        }

        if (partialQuarters.Length > 0)
        {
            if (looseYears.Length > 1)
            {
                return PmAnalyticsV2PeriodParse.UnsupportedResult;
            }

            RemoveMatches(ref residual, partialQuarters);
            RemoveMatches(ref residual, looseYears);
            return new PmAnalyticsV2PeriodParse(
                null,
                null,
                false,
                looseYears.Length == 1 ? ["Quarter"] : ["Quarter", "Year"]);
        }

        if (looseYears.Length == 1)
        {
            var year = int.Parse(looseYears[0].Value, CultureInfo.InvariantCulture);
            RemoveMatch(ref residual, looseYears[0]);
            RemoveMatches(ref residual, annualTokens);
            return new PmAnalyticsV2PeriodParse(
                new PmAnalyticsV2Period(PmAnalyticsV2PeriodKind.Year, Year: year),
                null,
                false,
                []);
        }

        if (looseYears.Length > 1)
        {
            return PmAnalyticsV2PeriodParse.UnsupportedResult;
        }

        if (annualTokens.Count > 0)
        {
            RemoveMatches(ref residual, annualTokens);
            return new PmAnalyticsV2PeriodParse(null, null, false, ["Year"]);
        }

        return PmAnalyticsV2PeriodParse.NoPeriod;
    }

    private static (T? Value, bool Ambiguous) ParseSingleFilter<T>(
        ref string residual,
        IReadOnlyList<(string Pattern, T Value)> choices)
        where T : struct, Enum
    {
        var filterText = residual;
        var candidates = choices
            .SelectMany(choice => FindMatches(filterText, choice.Pattern)
                .Select(match => (choice.Value, Match: match)));
        var selected = SelectLongestNonOverlapping(candidates);
        var values = selected.Select(item => item.Value).Distinct().ToArray();
        RemoveMatches(ref residual, selected.Select(item => item.Match));
        return values.Length > 1
            ? ((T?)null, true)
            : (values.Length == 0 ? (T?)null : (T?)values[0], false);
    }

    private static PmAnalyticsV2GroupBy ParseGroupBy(ref string residual, out bool ambiguous)
    {
        var candidates = new List<(PmAnalyticsV2GroupBy Value, Match Match)>();
        candidates.AddRange(
            FindMatches(
                residual,
                @"(?:grouped\s+by|by|across|for\s+all(?:\s+the)?)\s+departments?")
                .Select(match => (PmAnalyticsV2GroupBy.Department, match)));
        candidates.AddRange(FindMatches(residual, @"(?:grouped\s+by|by)\s+buildings?")
            .Select(match => (PmAnalyticsV2GroupBy.Building, match)));
        candidates.AddRange(FindMatches(residual, @"(?:grouped\s+by|by)\s+(?:pm\s+)?cycles?")
            .Select(match => (PmAnalyticsV2GroupBy.PmCycle, match)));
        var selected = SelectLongestNonOverlapping(candidates);
        var values = selected.Select(item => item.Value).Distinct().ToArray();
        ambiguous = values.Length > 1;
        RemoveMatches(ref residual, selected.Select(item => item.Match));
        return values.Length == 0 ? PmAnalyticsV2GroupBy.None : values[0];
    }

    private static PmAnalyticsV2Ranking? ParseRanking(ref string residual, out bool invalid)
    {
        invalid = false;
        var matches = RankingPattern.Matches(residual).Cast<Match>().ToArray();
        if (matches.Length == 0)
        {
            invalid = Regex.IsMatch(residual, @"\b(?:top|bottom|highest|lowest)\b", Options, RegexTimeout);
            return null;
        }

        if (matches.Length > 1)
        {
            invalid = true;
            return null;
        }

        var match = matches[0];
        var limit = int.Parse(match.Groups["limit"].Value, CultureInfo.InvariantCulture);
        var direction = match.Groups["direction"].Value.ToLowerInvariant() is "top" or "highest"
            ? PmAnalyticsV2Direction.Highest
            : PmAnalyticsV2Direction.Lowest;
        RemoveMatch(ref residual, match);
        if (limit is < 1 or > 10)
        {
            invalid = true;
            return null;
        }

        return new PmAnalyticsV2Ranking(limit, direction);
    }

    private static (PmAnalyticsV2Presentation? Value, bool Ambiguous) ParsePresentation(ref string residual)
    {
        var candidates = new List<(PmAnalyticsV2Presentation Value, Match Match)>();
        candidates.AddRange(FindMatches(residual, @"list|records?|sources?|evidence")
            .Select(match => (PmAnalyticsV2Presentation.Records, match)));
        candidates.AddRange(FindMatches(residual, @"percent(?:age)?|rate")
            .Select(match => (PmAnalyticsV2Presentation.Percent, match)));
        candidates.AddRange(FindMatches(residual, @"count|counts|number|how\s+many")
            .Select(match => (PmAnalyticsV2Presentation.Count, match)));
        var selected = SelectLongestNonOverlapping(candidates);
        var values = selected.Select(item => item.Value).Distinct().ToArray();
        RemoveMatches(ref residual, selected.Select(item => item.Match));
        return values.Length > 1
            ? (null, true)
            : (values.Length == 0 ? null : values[0], false);
    }

    private static PmAnalyticsV2CountBasis ParseCountBasis(ref string residual)
    {
        var matches = FindMatches(
            residual,
            @"(?:distinct|unique|different)(?=\s+(?:(?:non[- ]operational)\s+)?(?:assets?|fire[- ]extinguishers?|fire[- ]alarms?(?:\s+systems?)?|emergency[- ]lights?|water[- ]drinking[- ]stations?))");
        if (matches.Count == 0)
        {
            return PmAnalyticsV2CountBasis.AssetCycles;
        }

        RemoveMatches(ref residual, matches);
        return PmAnalyticsV2CountBasis.DistinctAssets;
    }

    private static IReadOnlyList<Match> FindMatches(string input, string pattern) =>
        CreateRegex(@"\b(?:" + pattern + @")\b").Matches(input).Cast<Match>().ToArray();

    private static List<(T Value, Match Match)> SelectLongestNonOverlapping<T>(
        IEnumerable<(T Value, Match Match)> candidates)
    {
        var selected = new List<(T Value, Match Match)>();
        foreach (var candidate in candidates
            .OrderByDescending(item => item.Match.Length)
            .ThenBy(item => item.Match.Index))
        {
            var start = candidate.Match.Index;
            var end = start + candidate.Match.Length;
            if (selected.Any(item =>
            {
                var otherStart = item.Match.Index;
                var otherEnd = otherStart + item.Match.Length;
                return start < otherEnd && otherStart < end;
            }))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected.OrderBy(item => item.Match.Index).ToList();
    }

    private static void RemoveMatches(ref string input, IEnumerable<Match> matches)
    {
        var characters = input.ToCharArray();
        foreach (var match in matches)
        {
            if (match.Index >= 0 && match.Length > 0 && match.Index + match.Length <= characters.Length)
            {
                Array.Fill(characters, ' ', match.Index, match.Length);
            }
        }

        input = new string(characters);
    }

    private static void RemoveMatch(ref string input, Match match) =>
        RemoveMatches(ref input, [match]);

    private static void RemoveLiteral(ref string input, string pattern) =>
        input = CreateRegex(pattern).Replace(input, " ");

    private static void RemoveLooseYears(ref string residual, out int yearCount)
    {
        var years = Regex.Matches(residual, @"\b20\d{2}\b", Options, RegexTimeout).Cast<Match>().ToArray();
        yearCount = years.Length;
        RemoveMatches(ref residual, years);
    }

    private static int ParseMonth(string value) => value.ToLowerInvariant() switch
    {
        "january" or "jan" => 1,
        "february" or "feb" => 2,
        "march" or "mar" => 3,
        "april" or "apr" => 4,
        "may" => 5,
        "june" or "jun" => 6,
        "july" or "jul" => 7,
        "august" or "aug" => 8,
        "september" or "sep" or "sept" => 9,
        "october" or "oct" => 10,
        "november" or "nov" => 11,
        _ => 12
    };

    private static string FormatCycle(Match match) =>
        match.Groups["year"].Value + "-" + match.Groups["month"].Value;

    private static string FormatMonthCycle(Match match) =>
        int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
        + "-"
        + ParseMonth(match.Groups["month"].Value).ToString("D2", CultureInfo.InvariantCulture);

    private static Regex CreateRegex(string pattern) => new(pattern, Options, RegexTimeout);

    private static PmAnalyticsV2QuestionParseResult Unsupported(string code, string detail) =>
        new(null, false, [], code, detail);

    private sealed record PmAnalyticsV2PeriodParse(
        PmAnalyticsV2Period? Period,
        PmAnalyticsV2Comparison? Comparison,
        bool Unsupported,
        IReadOnlyList<string> ClarificationFields)
    {
        internal static PmAnalyticsV2PeriodParse NoPeriod { get; } = new(null, null, false, []);
        internal static PmAnalyticsV2PeriodParse UnsupportedResult { get; } = new(null, null, true, []);
    }
}
