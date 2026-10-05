using System.Globalization;
using System.Text.RegularExpressions;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Reports;

internal sealed record PmAnalyticsQuestionAssessment(
    string SanitizedQuestion,
    string? ExplicitYear,
    int? ExplicitMonth,
    PmAnalyticsMetric? ExplicitMetric,
    string? ExplicitAssetCategory,
    string? ExplicitDepartment,
    bool GroupsByDepartment,
    PmAnalyticsInterpretationResult? FixedResult);

internal static class NaturalLanguageAnalyticsQuestionGuard
{
    private static readonly (string Alias, PmAnalyticsMetric Metric)[] MetricAliases =
    [
        ("progress", PmAnalyticsMetric.Progress), ("progreso", PmAnalyticsMetric.Progress),
        ("pag-usad", PmAnalyticsMetric.Progress),
        ("on-time compliance", PmAnalyticsMetric.OnTimeCompliance),
        ("pagsunod sa iskedyul", PmAnalyticsMetric.OnTimeCompliance),
        ("napapanahon", PmAnalyticsMetric.OnTimeCompliance),
        ("late inspections", PmAnalyticsMetric.CompletedLate),
        ("nahuling inspeksyon", PmAnalyticsMetric.CompletedLate),
        ("non-operational", PmAnalyticsMetric.NonOperational),
        ("hindi gumagana", PmAnalyticsMetric.NonOperational)
    ];

    private static readonly (string Alias, string Category)[] CategoryAliases =
    [
        ("fire extinguisher", AssetCategoryCatalog.FireExtinguisher),
        ("fire extinguishers", AssetCategoryCatalog.FireExtinguisher),
        ("fire-extinguisher", AssetCategoryCatalog.FireExtinguisher),
        ("pamatay-sunog", AssetCategoryCatalog.FireExtinguisher),
        ("fire alarm", AssetCategoryCatalog.FireAlarm),
        ("fire alarms", AssetCategoryCatalog.FireAlarm),
        ("fire alarm systems", AssetCategoryCatalog.FireAlarm),
        ("fire-alarm", AssetCategoryCatalog.FireAlarm),
        ("alarma sa sunog", AssetCategoryCatalog.FireAlarm),
        ("emergency light", AssetCategoryCatalog.EmergencyLight),
        ("emergency lights", AssetCategoryCatalog.EmergencyLight),
        ("emergency-light", AssetCategoryCatalog.EmergencyLight),
        ("ilaw pang-emergency", AssetCategoryCatalog.EmergencyLight),
        ("water drinking station", AssetCategoryCatalog.WaterDrinkingStation),
        ("water drinking stations", AssetCategoryCatalog.WaterDrinkingStation),
        ("water-drinking-station", AssetCategoryCatalog.WaterDrinkingStation),
        ("istasyon ng inuming tubig", AssetCategoryCatalog.WaterDrinkingStation)
    ];

    private static readonly (string Alias, int Month)[] MonthAliases =
    [
        ("january", 1), ("enero", 1), ("february", 2), ("pebrero", 2),
        ("march", 3), ("marso", 3), ("april", 4), ("abril", 4),
        ("mayo", 5), ("june", 6), ("hunyo", 6), ("july", 7), ("hulyo", 7),
        ("august", 8), ("agosto", 8), ("september", 9), ("setyembre", 9),
        ("october", 10), ("oktubre", 10), ("november", 11), ("nobyembre", 11),
        ("december", 12), ("disyembre", 12)
    ];
    private static readonly (string Alias, int Month)[] MonthAbbreviations =
    [
        ("jan", 1), ("ene", 1), ("feb", 2), ("peb", 2), ("mar", 3),
        ("apr", 4), ("abr", 4), ("may", 5), ("jun", 6), ("hun", 6),
        ("jul", 7), ("hul", 7), ("aug", 8), ("ago", 8), ("sep", 9),
        ("set", 9), ("oct", 10), ("okt", 10), ("nov", 11), ("dec", 12), ("dis", 12)
    ];

    private static readonly Regex Email = new(
        @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex Phone = new(
        @"(?<!\w)(?:\+?63|0)\s*[-.(]?(?:\d\s*[-().]?){9,10}(?!\w)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex Identifier = new(
        @"\b(?:employee|student|staff|personnel)\s*(?:id|no\.?|number)\s*[:#-]?\s*[A-Z0-9-]{4,}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex Year = new(
        @"(?<!\d)(?<year>\d{4})(?!\d)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex NumericCycle = new(
        @"(?<!\d)\d{4}-(?<month>0[1-9]|1[0-2])(?!\d)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex DepartmentFilter = new(
        @"\b(?:department|dept\.?|kagawaran)(?:\s+(?:of|ng))?\s+(?<name>""[^""]*""|[^,;.!?]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex DepartmentEnd = new(
        @"\b(?:for|in|during|group(?:ed)?\s+by|per|by|with|that|at)\b|\b\d{4}\b|\b(?:january|february|march|april|may|june|july|august|september|october|november|december|enero|pebrero|marso|abril|mayo|hunyo|hulyo|agosto|setyembre|oktubre|nobyembre|disyembre)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex GroupingClause = new(
        @"\b(?:group(?:ed)?\s+by|per|by each|for each|ayon sa|bawat)\s+(?<value>[\p{L}-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex PlainUnsupportedGrouping = new(
        @"\bby\s+(?:building|buildings|gusali|asset|category|month|cycle|status|year)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex UnsafeRequest = new(
        @"\b(?:ignore|disregard|override|forget)\b.{0,60}\b(?:instructions?|rules?|policy|system prompt)\b"
        + @"|\b(?:huwag|wag)\b.{0,30}\b(?:sundin|sumunod)\b.{0,60}\b(?:instructions?|tagubilin|patakaran)\b"
        + @"|\b(?:balewalain|isantabi)\b.{0,60}\b(?:instructions?|tagubilin|patakaran)\b"
        + @"|\b(?:sql|raw query|database query|select\s+.{1,40}\s+from|delete\s+from|drop\s+table|update\s+\w+\s+set)\b"
        + @"|\b(?:api[\s_-]*keys?|secrets?|passwords?|credentials?|(?:access|auth)\s*tokens?|private\s+data|personal\s+data|employee\s+records?|student\s+records?|personnel\s+records?|email\s+addresses?|phone\s+numbers?|contact\s+details?|pribadong\s+datos|personal\s+na\s+impormasyon|lihim|sikreto)\b"
        + @"|\b(?:reveal|show|expose|export|print|send|ibunyag|ilabas|ipakita)(?:me)?api[\s_-]*keys?\b"
        + @"|\b(?:ignore|disregard|override|forget|balewalain|isantabi)\b.{0,50}\b(?:role checks?|authorization|authentication|permissions?|access control|login checks?)\b"
        + @"|\b(?:bypass|circumvent|skip|evade|override|lampasan|iwasan)\b.{0,50}\b(?:authorization|authentication|permissions?|access control|login|roles?|pahintulot|pagpapatunay)\b"
        + @"|\b(?:without|walang)\b.{0,20}\b(?:authorization|authentication|permission|login|pahintulot)\b"
        + @"|\b(?:change|update|delete|drop|remove|edit|modify|create|approve|complete|close|baguhin|burahin|palitan|aprubahan|kumpletuhin|isara)\b.{0,60}\b(?:schedule|inspection|asset|maintenance record|pm record|records?|iskedyul|inspeksyon|tala|asset status)\b"
        + @"|\b(?:mark|set|itakda|markahan)\b.{0,40}\b(?:schedule|inspection|asset|record|iskedyul|inspeksyon|as\s+(?:complete|completed|acknowledged|closed))\b"
        + @"|\b(?:invent|fabricate|make up|guess|assume|pretend|gawa-gawa|imbentuhin|hulaan|magkunwari)\b.{0,60}\b(?:values?|numbers?|results?|counts?|percentages?|statistics|data|bilang|resulta|datos|porsiyento)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    internal static PmAnalyticsQuestionAssessment Assess(string question)
    {
        var sanitized = Sanitize(question.Trim());
        if (UnsafeRequest.IsMatch(sanitized))
        {
            return Fixed(sanitized, PmAnalyticsInterpretationStatus.Unsupported, "UnsafeRequestNotSupported");
        }

        if (HasComparisonIntent(sanitized))
        {
            return Fixed(sanitized, PmAnalyticsInterpretationStatus.Unsupported, "ComparisonNotSupported");
        }

        if (HasUnsupportedGrouping(sanitized))
        {
            return Fixed(sanitized, PmAnalyticsInterpretationStatus.Unsupported, "GroupingNotSupported");
        }

        var metrics = FindMetrics(sanitized);
        var categories = FindCategories(sanitized);
        var years = Year.Matches(sanitized)
            .Select(match => match.Groups["year"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var months = FindMonths(sanitized);
        var department = FindDepartment(
            sanitized,
            out var ambiguousDepartment,
            out var unsafeDepartment);
        var groupByDepartment = HasDepartmentGrouping(sanitized);
        var clarifications = new List<PmAnalyticsClarificationField>();

        if (unsafeDepartment)
        {
            return Fixed(sanitized, PmAnalyticsInterpretationStatus.Unsupported, "UnsafeDepartment");
        }

        if (metrics.Count > 1)
        {
            clarifications.Add(PmAnalyticsClarificationField.Metric);
        }

        if (categories.Count > 1)
        {
            clarifications.Add(PmAnalyticsClarificationField.AssetCategory);
        }

        if (years.Length > 1)
        {
            clarifications.Add(PmAnalyticsClarificationField.Year);
        }

        if (months.Count > 1)
        {
            clarifications.Add(PmAnalyticsClarificationField.Month);
        }

        if (ambiguousDepartment)
        {
            clarifications.Add(PmAnalyticsClarificationField.Department);
        }

        if (HasAmbiguousGrouping(sanitized))
        {
            clarifications.Add(PmAnalyticsClarificationField.GroupBy);
        }

        if (clarifications.Count > 0)
        {
            return new PmAnalyticsQuestionAssessment(
                sanitized,
                years.Length == 1 ? years[0] : null,
                months.Count == 1 ? months[0] : null,
                metrics.Count == 1 ? metrics[0] : null,
                categories.Count == 1 ? categories[0] : null,
                department,
                groupByDepartment,
                new PmAnalyticsInterpretationResult(
                    PmAnalyticsInterpretationStatus.NeedsClarification,
                    null,
                    clarifications,
                    null,
                    null));
        }

        if (years.Length == 1 && months.Count == 1
            && categories.Count == 1
            && !CpmpScheduleFrequency.IsValid(categories[0], months[0]))
        {
            return Fixed(sanitized, PmAnalyticsInterpretationStatus.Unsupported, "UnsupportedPmCycle");
        }

        return new PmAnalyticsQuestionAssessment(
            sanitized,
            years.Length == 1 ? years[0] : null,
            months.Count == 1 ? months[0] : null,
            metrics.Count == 1 ? metrics[0] : null,
            categories.Count == 1 ? categories[0] : null,
            department,
            groupByDepartment,
            null);
    }

    internal static bool MatchesExplicitScope(
        PmAnalyticsQuestionAssessment assessment,
        PmAnalyticsPlan plan)
    {
        if ((assessment.ExplicitYear is not null && assessment.ExplicitYear != plan.PmCycle[..4])
            || (assessment.ExplicitMonth is not null
                && assessment.ExplicitMonth != int.Parse(plan.PmCycle[5..], CultureInfo.InvariantCulture))
            || (assessment.ExplicitMetric is not null && assessment.ExplicitMetric != plan.Metric)
            || (assessment.ExplicitAssetCategory is not null
                && assessment.ExplicitAssetCategory != plan.AssetCategory)
            || (assessment.ExplicitDepartment is not null
                && assessment.ExplicitDepartment != plan.Department)
            || (plan.Department is not null && assessment.ExplicitDepartment is null)
            || (assessment.GroupsByDepartment != (plan.GroupBy == PmAnalyticsGroupBy.Department)))
        {
            return false;
        }

        return true;
    }

    internal static PmAnalyticsInterpretationResult NeedsClarification(
        params PmAnalyticsClarificationField[] fields)
    {
        return new PmAnalyticsInterpretationResult(
            PmAnalyticsInterpretationStatus.NeedsClarification,
            null,
            fields,
            null,
            null);
    }

    internal static PmAnalyticsPresentation PresentationFor(
        string question,
        PmAnalyticsMetric metric)
    {
        if (metric is PmAnalyticsMetric.CompletedLate or PmAnalyticsMetric.NonOperational)
        {
            return PmAnalyticsPresentation.Count;
        }

        if (metric == PmAnalyticsMetric.OnTimeCompliance)
        {
            return PmAnalyticsPresentation.Percent;
        }

        return Regex.IsMatch(
            question,
            @"\bhow many\b|\bnumber of\b|\bcount(?: of)?\b|\btotal of\b|\bilang ng\b|\bilan(?:g)?\b|\bdami ng\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100))
            ? PmAnalyticsPresentation.Count
            : PmAnalyticsPresentation.Percent;
    }

    private static PmAnalyticsQuestionAssessment Fixed(
        string question,
        PmAnalyticsInterpretationStatus status,
        string code)
    {
        return new PmAnalyticsQuestionAssessment(
            question,
            null,
            null,
            null,
            null,
            null,
            false,
            new PmAnalyticsInterpretationResult(status, null, [], null, code));
    }

    private static string Sanitize(string value)
    {
        // These patterns mask common email, phone, and labeled-ID shapes. They do not detect arbitrary personal names.
        var masked = Email.Replace(value, "[EMAIL]");
        masked = Phone.Replace(masked, "[PHONE]");
        return Identifier.Replace(masked, "[IDENTIFIER]");
    }

    private static bool HasComparisonIntent(string question) => Regex.IsMatch(
        question,
        @"\b(?:compare|comparison|versus|vs\.?|ikumpara|paghambingin|kumpara)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static bool HasUnsupportedGrouping(string question)
    {
        return PlainUnsupportedGrouping.IsMatch(question)
            || GroupingClause.Matches(question)
            .Cast<Match>()
            .Select(match => match.Groups["value"].Value)
            .Any(value => !IsDepartmentTerm(value));
    }

    private static bool HasAmbiguousGrouping(string question) => Regex.IsMatch(
        question,
        @"\b(?:group(?:ed)?\s+by|per|by each|for each|ayon sa|bawat)\s+(?:department|dept\.?|kagawaran)\b.{0,60}\b(?:or|o)\b.{0,60}\b(?:no grouping|without grouping|ungrouped|none)\b|\b(?:no grouping|without grouping|ungrouped|none)\b.{0,60}\b(?:or|o)\b.{0,60}\b(?:group(?:ed)?\s+by|per|by each|for each|ayon sa|bawat)\s+(?:department|dept\.?|kagawaran)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static bool IsDepartmentTerm(string value) => value.Equals(
        "department",
        StringComparison.OrdinalIgnoreCase)
        || value.Equals("dept", StringComparison.OrdinalIgnoreCase)
        || value.Equals("kagawaran", StringComparison.OrdinalIgnoreCase);

    private static List<PmAnalyticsMetric> FindMetrics(string question)
    {
        return MetricAliases
            .Where(alias => ContainsAlias(question, alias.Alias))
            .Select(alias => alias.Metric)
            .Distinct()
            .ToList();
    }

    private static List<string> FindCategories(string question)
    {
        return CategoryAliases
            .Where(alias => ContainsAlias(question, alias.Alias))
            .Select(alias => alias.Category)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<int> FindMonths(string question)
    {
        var months = MonthAliases
            .Where(alias => ContainsAlias(question, alias.Alias))
            .Select(alias => alias.Month)
            .Distinct()
            .ToList();
        foreach (var abbreviation in MonthAbbreviations)
        {
            var token = Regex.Escape(abbreviation.Alias);
            var hasExplicitContext = Regex.IsMatch(
                question,
                $@"(?<![\p{{L}}]){token}\.?\s*,?\s*\d{{4}}\b|\b\d{{4}}\s+{token}\.?(?![\p{{L}}])|\b(?:in|during|sa|noong)\s+{token}\.?(?![\p{{L}}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            if (hasExplicitContext && !months.Contains(abbreviation.Month))
            {
                months.Add(abbreviation.Month);
            }
        }

        foreach (Match match in NumericCycle.Matches(question))
        {
            var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
            if (!months.Contains(month))
            {
                months.Add(month);
            }
        }

        return months;
    }

    private static bool ContainsAlias(string question, string alias) => Regex.IsMatch(
        question,
        $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(alias)}(?![\p{{L}}\p{{N}}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static string? FindDepartment(
        string question,
        out bool ambiguous,
        out bool unsafeDepartment)
    {
        ambiguous = false;
        unsafeDepartment = false;
        var matches = DepartmentFilter.Matches(question)
            .Cast<Match>()
            .Where(match => !IsGroupingDepartment(question, match.Index))
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        var labels = new List<string>();
        if (matches.Any(match => match.Groups["name"].Value.StartsWith('"'))
            && question.Count(character => character == '"') != 2)
        {
            unsafeDepartment = true;
            return null;
        }

        foreach (var match in matches)
        {
            var raw = match.Groups["name"].Value.Trim();
            if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            {
                raw = raw[1..^1];
            }
            else
            {
                var stop = DepartmentEnd.Match(raw);
                if (stop.Success)
                {
                    raw = raw[..stop.Index].Trim();
                }
            }

            var alternatives = Regex.Split(
                raw,
                @"\s+(?:or|o)\s+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            ambiguous |= alternatives.Length > 1;
            labels.AddRange(alternatives.Select(label => label.Trim().Trim('.', ',', ';', ':', '-', ' ')));
        }

        var distinct = labels.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ambiguous |= matches.Length > 1 || distinct.Length != 1;
        if (distinct.Length != 1)
        {
            return null;
        }

        var labelValue = distinct[0];
        if (labelValue.Length is 0 or > 256
            || labelValue.Any(char.IsControl)
            || labelValue.Contains('"')
            || labelValue.Contains('\\')
            || labelValue.Any(character => !(char.IsLetterOrDigit(character)
                || char.IsWhiteSpace(character)
                || character is '&' or '-' or '.' or '\'')))
        {
            unsafeDepartment = true;
            return null;
        }

        return labelValue.ToUpperInvariant();
    }

    private static bool IsGroupingDepartment(string question, int departmentIndex)
    {
        var prefix = question[..departmentIndex];
        return Regex.IsMatch(
            prefix,
            @"\b(?:group(?:ed)?\s+by|per|by|ayon sa|bawat)\s+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }

    private static bool HasDepartmentGrouping(string question) => Regex.IsMatch(
        question,
        @"\b(?:group(?:ed)?\s+by|per|by each|by|ayon sa|bawat)\s+(?:department|dept\.?|kagawaran)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
}
