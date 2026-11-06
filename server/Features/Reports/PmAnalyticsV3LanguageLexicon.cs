using System.Globalization;
using System.Text.RegularExpressions;

namespace UniPM.Api.Features.Reports;

internal readonly record struct PmAnalyticsV3LanguageMatch(string Id, int Index, int Length);

/// <summary>
/// Bounded phrase recognition for the V3 report contract. It records intent
/// slots without rewriting the user's question.
/// </summary>
internal static class PmAnalyticsV3LanguageLexicon
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    internal const string MonthPattern =
        @"january|jan|enero|ene|february|feb|pebrero|peb|march|mar|marso|april|apr|abril|abr|may|mayo|june|jun|hunyo|hun|july|jul|hulyo|hul|august|aug|agosto|ago|september|sept|sep|setyembre|set|october|oct|oktubre|okt|november|nov|nobyembre|nob|december|dec|disyembre|dis";

    internal const string NumberPattern =
        @"\d{1,3}(?:\.\d+)?|zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|isa|dalawa|dalawang|tatlo|tatlong|apat|lima|limang|anim|pito|walo|siyam|sampu|sampung|dalawampu";

    private static readonly Regex MonthRegex = new(
        $@"(?<![\p{{L}}\p{{N}}])(?<month>{MonthPattern})(?![\p{{L}}\p{{N}}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly IReadOnlyDictionary<string, int> Months =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["january"] = 1,
            ["jan"] = 1,
            ["enero"] = 1,
            ["ene"] = 1,
            ["february"] = 2,
            ["feb"] = 2,
            ["pebrero"] = 2,
            ["peb"] = 2,
            ["march"] = 3,
            ["mar"] = 3,
            ["marso"] = 3,
            ["april"] = 4,
            ["apr"] = 4,
            ["abril"] = 4,
            ["abr"] = 4,
            ["may"] = 5,
            ["mayo"] = 5,
            ["june"] = 6,
            ["jun"] = 6,
            ["hunyo"] = 6,
            ["hun"] = 6,
            ["july"] = 7,
            ["jul"] = 7,
            ["hulyo"] = 7,
            ["hul"] = 7,
            ["august"] = 8,
            ["aug"] = 8,
            ["agosto"] = 8,
            ["ago"] = 8,
            ["september"] = 9,
            ["sept"] = 9,
            ["sep"] = 9,
            ["setyembre"] = 9,
            ["set"] = 9,
            ["october"] = 10,
            ["oct"] = 10,
            ["oktubre"] = 10,
            ["okt"] = 10,
            ["november"] = 11,
            ["nov"] = 11,
            ["nobyembre"] = 11,
            ["nob"] = 11,
            ["december"] = 12,
            ["dec"] = 12,
            ["disyembre"] = 12,
            ["dis"] = 12
        };

    private sealed record CategoryAlias(string Id, string Pattern, bool RequiresReportingContext = false);

    private static readonly CategoryAlias[] CategoryAliases =
    [
        new("fire-extinguisher", @"fire[\s-]+extinguishers?"),
        new("fire-extinguisher", @"extinguishers?(?:\s+checks?)?"),
        new("fire-extinguisher", @"pamatay[\s-]+sunog", RequiresReportingContext: true),
        new("fire-extinguisher", @"fire-extinguisher"),
        new("fire-alarm", @"fire[\s-]+alarms?\s+systems?"),
        new("fire-alarm", @"fire[\s-]+alarms?", RequiresReportingContext: true),
        new("fire-alarm", @"alarm[\s-]+systems?", RequiresReportingContext: true),
        new("fire-alarm", @"alarma\s+sa\s+sunog", RequiresReportingContext: true),
        new("fire-alarm", @"fire-alarm"),
        new("emergency-light", @"emergency[\s-]+lights?"),
        new("emergency-light", @"ilaw\s+(?:pang[\s-]?|para\s+sa\s+)emergency", RequiresReportingContext: true),
        new("emergency-light", @"emergency-light"),
        new("water-drinking-station", @"water\s+drinking\s+stations?"),
        new("water-drinking-station", @"drinking[\s-]+stations?", RequiresReportingContext: true),
        new("water-drinking-station", @"istasyon\s+ng\s+inuming\s+tubig", RequiresReportingContext: true),
        new("water-drinking-station", @"inuman\s+ng\s+tubig", RequiresReportingContext: true),
        new("water-drinking-station", @"water-drinking-station")
    ];

    private static readonly Regex ReportingContext = new(
        @"\b(?:asset|equipment|registered|registration|inventory|schedule|scheduled|pm|maintenance|inspection|inspections|inspect(?:ed|ion)?|check(?:s|ed)?|compliance|progress|completed|missed|overdue|late|count|record|records|list|show|display|which|system|sistema|iskedyul|inspeksyon|inspeksiyon|rehistro|nakarehistro|bilang|ilan|ilang|alin|aling|pakilista|ipakita)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private sealed record MeasureAlias(string Id, string Pattern);

    private static readonly MeasureAlias[] MeasureAliases =
    [
        new(PmAnalyticsV3Catalog.DistinctAssetCount, @"\bdistinct\s+assets?\b"),
        new(PmAnalyticsV3Catalog.ScheduledCount, @"\bscheduled\b(?!\s+pm[- ]?cycles?\b)"),
        new(PmAnalyticsV3Catalog.ScheduledCount, @"\b(?:nakaiskedyul|naka[- ]iskedyul|may\s+(?:pm\s+)?schedule|may\s+iskedyul|nakaplanong\s+inspeksyon)\b"),
        new(PmAnalyticsV3Catalog.CompletedLateCount, @"\bcompleted[ -]late\b|\blate\s+(?:inspections?|findings?)\b|\blate(?:\s+(?!(?:and|or|not|no|without|except|excluding)\b)[\p{L}\p{N}-]+){1,4}\s+inspections?\b"),
        new(PmAnalyticsV3Catalog.CompletedLateCount, @"\b(?:late\s+na\s+natapos|natapos\s+nang?\s+late|na[- ]inspect(?:ed)?\s+nang?\s+lampas\s+sa\s+(?:deadline|takdang\s+oras)|na[- ]inspect(?:ed)?\s+after\s+(?:the\s+)?(?:deadline|due\s+date))\b"),
        new(PmAnalyticsV3Catalog.CompletedOnTimeCount, @"\bcompleted\s+on\s+time\b|\bon-time\s+count\b"),
        new(PmAnalyticsV3Catalog.NonOperationalCount, @"\bnon[ -]operational\b"),
        new(PmAnalyticsV3Catalog.NonOperationalCount, @"\bhindi\s+(?:gumagana|operational)\b.{0,24}\b(?:sa\s+)?(?:inspection|inspeksyon|inspeksiyon|finding|finding\s+sa\s+inspection)\b|\b(?:inspection|inspeksyon|inspeksiyon)\b.{0,24}\bhindi\s+(?:gumagana|operational)\b"),
        new(PmAnalyticsV3Catalog.AssignmentOverdueCount, @"\boverdue\s+inspector\s+assignment\s+counts?\b|\binspector\s+assignment\s+(?:is\s+)?overdue\b"),
        new(PmAnalyticsV3Catalog.AssignmentCompletedWithinCount, @"\bcompleted\s+within\s+(?:the\s+)?inspector\s+assignment\s+period\b"),
        new(PmAnalyticsV3Catalog.AssignmentCompletedLateCount, @"\bcompleted\s+after\s+(?:the\s+)?inspector\s+assignment\s+period\b"),
        new(PmAnalyticsV3Catalog.AssignmentOpenCount, @"\bopen\s+inspector\s+assignments?\b"),
        new(PmAnalyticsV3Catalog.AssignmentUnknownCount, @"\bunknown\s+inspector\s+assignment\s+counts?\b"),
        new(PmAnalyticsV3Catalog.MissedCount, @"\bmissed\b|\boverdue\b(?!\s+inspector\s+assignment)"),
        new(PmAnalyticsV3Catalog.MissedCount, @"\blampas\s+na\s+sa\s+(?:deadline|takdang\s+oras)\b.{0,32}\b(?:hindi|di)\s+(?:pa\s+)?(?:tapos|natapos|na[- ]inspect|nainspeks(?:yon|iyon))\b|\b(?:hindi|di)\s+(?:pa\s+)?(?:natapos|na[- ]inspect|nainspeks(?:yon|iyon))\b.{0,32}\b(?:lampas|past|after)\s+(?:na\s+)?(?:sa\s+)?(?:deadline|takdang\s+oras)\b"),
        new(PmAnalyticsV3Catalog.RemainingCount, @"\bremaining\b"),
        new(PmAnalyticsV3Catalog.OnTimeCompliancePercent, @"\bon-time\s+compliance\b|\bcompliance\b|\bpagsunod\s+sa\s+(?:iskedyul|deadline|takdang\s+oras)\b|\bnasunod\s+ang\s+deadline\b"),
        new(PmAnalyticsV3Catalog.ProgressPercent, @"\bwhat\s+percentage\b.{0,120}\b(?:finished|completed|natapos|nagawa)\b|\bpercentage\s+of\b.{0,120}\b(?:finished|completed|natapos|nagawa)\b"),
        new(PmAnalyticsV3Catalog.CompletedCount, @"\bcompleted\b(?![ -]late\b|\s+on\s+time\b|\s+within\s+(?:the\s+)?inspector\b)|\binspected\b|\binspection\s+counts?\b|\bnumber\s+of\s+inspections\b"),
        new(PmAnalyticsV3Catalog.CompletedCount, @"\b(?:na[- ]inspect(?:ed)?|nai[- ]inspect|nainspeks(?:yon|iyon)|na-inspeks(?:yon|iyon)|natapos|nagawa)\b"),
        new(PmAnalyticsV3Catalog.ProgressPercent, @"\bprogress\b|\bcompletion\s+progress\b|\bporsiyento\s+ng\s+natapos\b|\bporsyento\s+ng\s+natapos\b")
    ];

    private static readonly Regex InventoryIntent = new(
        @"\b(?:asset\s+inventory|inventory|registered|registration\s+status|asset\s+count|asset\s+register|current\s+(?:asset\s+)?registry|nakarehistro(?:ng)?|rehistrado(?:ng)?|narehistro(?:ng)?|rehistro)\b|\bnakatala\s+(?:sa|bilang\s+)?(?:rehistro|asset|kagamitan)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PmExecutionIntent = new(
        @"\b(?:pm|preventive\s+maintenance|schedules?|scheduled|inspections?|inspect(?:ed|ion)?|checks?|inspector\s+assignments?|falling\s+behind|progress|overdue|compliance|on[ -]?time|late|missed|non[ -]?operational|operational|completed|natapos|nagawa|inspeksyon|inspeksiyon|pagsusuri|na[- ]inspect(?:ed)?|nai[- ]inspect|nainspeks(?:yon|iyon)|iskedyul|nakaiskedyul|naka[- ]iskedyul|nakaplanong\s+inspeksyon)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex CountIntent = new(
        @"\b(?:how\s+many|how\s+much|count|number\s+of|total|ilang|ilan|bilang|kabuuang\s+bilang)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex RecordListIntent = new(
        @"\bwhich\s+(?:assets?|equipment|units?|source\s+rows?)\b|^\s*which\s+(?:fire[\s-]+extinguishers?|extinguishers?|fire[\s-]+alarms?(?:\s+systems?)?|alarms?|emergency[\s-]+lights?|water\s+drinking\s+stations?|drinking[\s-]+stations?)\b|\b(?:list|show|display)\s+(?:me\s+)?(?:the\s+)?(?:actual\s+)?(?:affected\s+)?(?:assets?|equipment|units?|records?|source\s+rows?)\b|\bshow\s+me\s+the\s+actual\s+records\b|\bindividual\s+assets?\b|\b(?:alin|aling)\b.{0,32}\b(?:mga\s+)?(?:asset|kagamitan|unit|record|fire|pamatay|alarma|emergency|ilaw|water|inuman)\b|\banu[- ]ano\b|\b(?:pakilista|ilista)\b|\bipakita\s+ang\s+(?:mga|aktuwal\s+na\s+mga?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex PlainRecordRequest = new(
        @"\b(?:records?|source\s+rows?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex UnsupportedSpecificCategoryCue = new(
        @"\b(?:water\s+dispensers?|water\s+fountains?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex NamedPlaceEquipmentWord = new(
        @"\b(?:fe|alarm|light)\s+(?:department|building|location)\b|\b(?:department|building|location)\s+(?:of\s+)?(?:fe|alarm|light)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex IncompleteWorkIntent = new(
        @"\b(?:unfinished|incomplete|not\s+finished|not\s+completed|pending)\b|\b(?:hindi|di)\s+(?:pa\s+)?(?:tapos|natapos|na[- ]inspect|nainspeks(?:yon|iyon))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex OverdueIntent = new(
        @"\b(?:overdue|missed)\b|\b(?:unfinished|incomplete|not\s+finished|not\s+completed)\b.{0,40}\b(?:past|after|beyond)\s+(?:the\s+)?(?:deadline|due\s+date)\b|\blampas\s+na\s+sa\s+(?:deadline|takdang\s+oras)\b|\b(?:hindi|di)\s+(?:pa\s+)?(?:natapos|na[- ]inspect|nainspeks(?:yon|iyon))\b.{0,32}\b(?:lampas|past|after)\s+(?:sa\s+)?(?:deadline|takdang\s+oras)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex CompletedLateIntent = new(
        @"\b(?:completed[ -]late|late\s+(?:inspections?|findings?|records?)|late\s+na\s+natapos|natapos\s+nang?\s+late|(?:finished|completed)\s+(?:past|after|beyond)\s+(?:the\s+)?(?:deadline|due\s+date)|na[- ]inspect(?:ed)?\s+nang?\s+lampas\s+sa\s+(?:deadline|takdang\s+oras))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex RelativeCurrentYear = new(
        @"\bthis\s+year\b|\b(?:ngayong|nitong)\s+taon\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex RelativePreviousYear = new(
        @"\blast\s+year\b|\b(?:noong\s+)?nakaraang\s+taon\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex DateContext = new(
        @"\b(?:in|during|for|on|noong|ngayong|sa\s+buwan\s+ng|pm\s+cycle|cycle|calendar\s+month|month|buwan)\s*$|^\s*(?:pm\s+cycle|cycle|calendar\s+month|month|buwan|pm)\b|\b\d{4}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    internal static IReadOnlyList<PmAnalyticsV3LanguageMatch> FindCategories(string question)
    {
        var matches = new List<PmAnalyticsV3LanguageMatch>();
        foreach (var alias in CategoryAliases)
        {
            var pattern = $@"(?<![\p{{L}}\p{{N}}])(?:{alias.Pattern})(?![\p{{L}}\p{{N}}])";
            foreach (Match match in Regex.Matches(
                         question,
                         pattern,
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                         RegexTimeout))
            {
                if (alias.Id == "fire-alarm"
                    && alias.Pattern == @"alarms?"
                    && NamedPlaceEquipmentWord.IsMatch(question))
                {
                    continue;
                }

                if (alias.RequiresReportingContext && !HasReportingContext(question, match.Index, match.Length))
                {
                    continue;
                }

                matches.Add(new PmAnalyticsV3LanguageMatch(alias.Id, match.Index, match.Length));
            }
        }

        return matches
            .OrderBy(match => match.Index)
            .ThenByDescending(match => match.Length)
            .GroupBy(match => (match.Id, match.Index))
            .Select(group => group.First())
            .ToArray();
    }

    internal static IReadOnlyList<string> ResolveCategories(string question) => FindCategories(question)
        .Select(match => match.Id)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    internal static bool HasAllCategories(string question) => Regex.IsMatch(
        question,
        @"\ball\s+(?:four\s+)?(?:supported\s+)?categories\b|\blahat\s+ng\s+(?:apat\s+na\s+)?(?:supported\s+)?(?:categories|kategorya)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    internal static bool HasInventoryIntent(string question) => InventoryIntent.IsMatch(question);

    internal static bool HasPmExecutionIntent(string question) => PmExecutionIntent.IsMatch(question);

    internal static bool HasCountIntent(string question) => CountIntent.IsMatch(question);

    internal static bool HasListIntent(string question) => RecordListIntent.IsMatch(question);

    internal static bool HasMixedCountAndListIntent(string question) =>
        HasCountIntent(question) && HasListIntent(question);

    internal static bool HasUnresolvedCategoryCue(string question)
    {
        if (HasAmbiguousPlaceValue(question) || NamedPlaceEquipmentWord.IsMatch(question))
        {
            return false;
        }

        if (UnsupportedSpecificCategoryCue.IsMatch(question)
            && (HasPmExecutionIntent(question) || HasInventoryIntent(question)))
        {
            return true;
        }

        var equipmentContext = Regex.IsMatch(
            question,
            @"\b(?:asset|equipment|registered|registration|inventory|schedule|scheduled|pm|maintenance|inspection|inspections|inspect(?:ed|ion)?|check(?:s|ed)?|compliance|progress|completed|missed|overdue|late|category|kagamitan|nakarehistro|iskedyul|inspeksyon|bilang|ilan|ilang)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);
        return equipmentContext
            && (Regex.IsMatch(question, @"\b(?:fe|alarm|light)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)
                && (HasPmExecutionIntent(question) || Regex.IsMatch(question, @"\b(?:assets?|equipment|registered|inventory|kagamitan|nakarehistro)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)));
    }

    internal static bool HasIncompleteWorkIntent(string question) => IncompleteWorkIntent.IsMatch(question);

    internal static bool HasOverdueIntent(string question) => OverdueIntent.IsMatch(question);

    internal static bool HasCompletedLateIntent(string question) => CompletedLateIntent.IsMatch(question);

    internal static bool IsRecordsQuestion(string question) =>
        HasListIntent(question) && !HasCountIntent(question)
        || PlainRecordRequest.IsMatch(question) && !HasCountIntent(question);

    internal static IReadOnlyList<PmAnalyticsV3LanguageMatch> FindMeasureMentions(string question)
    {
        var matches = new List<PmAnalyticsV3LanguageMatch>();
        foreach (var alias in MeasureAliases)
        {
            foreach (Match match in Regex.Matches(
                         question,
                         alias.Pattern,
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                         RegexTimeout))
            {
                if (alias.Id == PmAnalyticsV3Catalog.MissedCount
                        && match.Value.Equals("overdue", StringComparison.OrdinalIgnoreCase)
                        && Regex.IsMatch(
                            question[..match.Index],
                        @"\binspector\s+assignment(?:\s+is)?\s*$",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                            RegexTimeout))
                {
                    continue;
                }

                matches.Add(new PmAnalyticsV3LanguageMatch(alias.Id, match.Index, match.Length));
            }
        }

        return matches.OrderBy(match => match.Index).ThenBy(match => match.Length).ToArray();
    }

    internal static IReadOnlyList<string> ResolveMeasures(string question)
    {
        var mentions = FindMeasureMentions(question);
        var completedCountMentions = mentions
            .Where(match => match.Id == PmAnalyticsV3Catalog.CompletedCount)
            .ToArray();
        var progressMentions = mentions
            .Where(match => match.Id == PmAnalyticsV3Catalog.ProgressPercent)
            .ToArray();
        var hasStandaloneCompletedCount = completedCountMentions.Any(completed =>
            !progressMentions.Any(progress => progress.Index <= completed.Index
                && progress.Index + progress.Length >= completed.Index + completed.Length));
        var hasSpecificCompletedMeasure = mentions.Any(match => match.Id is PmAnalyticsV3Catalog.CompletedLateCount
            or PmAnalyticsV3Catalog.CompletedOnTimeCount
            or PmAnalyticsV3Catalog.AssignmentCompletedWithinCount
            or PmAnalyticsV3Catalog.AssignmentCompletedLateCount);

        return mentions
            .GroupBy(match => match.Id, StringComparer.Ordinal)
            .Select(group => group.OrderBy(match => match.Index).First())
            .OrderBy(match => match.Index)
            .Select(match => match.Id)
            .Where(id => id != PmAnalyticsV3Catalog.CompletedCount
                || !hasSpecificCompletedMeasure && hasStandaloneCompletedCount)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<int> FindMonths(string question)
    {
        var result = new List<int>();
        foreach (Match match in MonthRegex.Matches(question))
        {
            var alias = match.Groups["month"].Value;
            if (alias.Equals("may", StringComparison.OrdinalIgnoreCase)
                && !HasMayDateContext(question, match.Index, match.Length))
            {
                continue;
            }

            var month = Months[alias];
            if (!result.Contains(month)) result.Add(month);
        }

        return result;
    }

    internal static bool HasRelativeYear(string question) =>
        RelativeCurrentYear.IsMatch(question) || RelativePreviousYear.IsMatch(question);

    internal static int? ResolveRelativeYear(string question, DateTimeOffset nowUtc)
    {
        var current = RelativeCurrentYear.IsMatch(question);
        var previous = RelativePreviousYear.IsMatch(question);
        if (current == previous) return null;

        var manilaNow = nowUtc.ToOffset(TimeSpan.FromHours(8));
        return current ? manilaNow.Year : manilaNow.Year - 1;
    }

    internal static bool TryParseNumber(string value, out int number)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return number is >= 0 and <= 999;
        }

        number = value.Trim().ToLowerInvariant() switch
        {
            "one" or "isa" => 1,
            "two" or "dalawa" or "dalawang" => 2,
            "three" or "tatlo" or "tatlong" => 3,
            "four" or "apat" => 4,
            "five" or "lima" or "limang" => 5,
            "six" or "anim" => 6,
            "seven" or "pito" => 7,
            "eight" or "walo" => 8,
            "nine" or "siyam" => 9,
            "ten" or "sampu" or "sampung" => 10,
            "eleven" => 11,
            "twelve" => 12,
            "thirteen" => 13,
            "fourteen" => 14,
            "fifteen" => 15,
            "sixteen" => 16,
            "seventeen" => 17,
            "eighteen" => 18,
            "nineteen" => 19,
            "twenty" or "dalawampu" => 20,
            _ => -1
        };
        return number >= 0;
    }

    internal static bool HasRankingIntent(string question) => Regex.IsMatch(
        question,
        @"\b(?:top|bottom|highest|lowest|(?<!at\s)most|(?<!at\s)least|rank(?:ed|ing)?|pinakamarami(?:ng)?|pinakakaunti|pinakamataas|pinakamababa)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeout);

    internal static Match FindTop(string question)
    {
        const string direction = @"top|bottom|highest|lowest|(?<!at\s)most|(?<!at\s)least|rank(?:ed|ing)?|pinakamarami(?:ng)?|pinakakaunti|pinakamataas|pinakamababa";
        const string group = @"departments?|department|departamento|kagawaran|buildings?|building|gusali|categor(?:y|ies)|kategorya";
        var leadingLimit = Regex.Match(
            question,
            $@"\b(?:which|alin(?:g)?)\s+(?<limit>{NumberPattern})\s+(?:{group})\b.{{0,120}}?\b(?<direction>{direction})\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);
        return leadingLimit.Success
            ? leadingLimit
            : Regex.Match(
                question,
                $@"\b(?<direction>{direction})(?:\s+(?<limit>{NumberPattern}))?\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexTimeout);
    }

    internal static bool TryResolveNearestMeasure(
        string question,
        int start,
        int length,
        IReadOnlyList<string> allowedMeasures,
        out string measure)
    {
        measure = string.Empty;
        var end = start + length;
        var nearby = FindMeasureMentions(question)
            .Where(match => allowedMeasures.Contains(match.Id, StringComparer.Ordinal))
            .Select(match => new
            {
                match.Id,
                Distance = match.Index > end ? match.Index - end : start > match.Index + match.Length ? start - (match.Index + match.Length) : 0
            })
            .Where(match => match.Distance <= 64)
            .OrderBy(match => match.Distance)
            .ThenBy(match => match.Id, StringComparer.Ordinal)
            .ToArray();
        if (nearby.Length == 0)
        {
            return false;
        }

        var closest = nearby[0].Distance;
        var candidates = nearby.Where(match => match.Distance == closest)
            .Select(match => match.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length != 1) return false;

        measure = candidates[0];
        return true;
    }

    internal static bool HasMeasureNear(string question, int start, int length) =>
        TryResolveNearestMeasure(
            question,
            start,
            length,
            ResolveMeasures(question),
            out _);

    internal static bool IsSupportedCategoryUnion(string question, int operatorIndex, int operatorLength)
    {
        var leftText = question[..operatorIndex];
        var rightText = question[(operatorIndex + operatorLength)..];
        var leftCategoryMatches = FindCategories(leftText);
        var rightCategoryMatches = FindCategories(rightText);
        if (leftCategoryMatches.Select(match => match.Id).Distinct(StringComparer.Ordinal).Count() != 1
            || rightCategoryMatches.Select(match => match.Id).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            return false;
        }
        var leftCategory = leftCategoryMatches.OrderByDescending(match => match.Length).First();
        var rightCategory = rightCategoryMatches.OrderByDescending(match => match.Length).First();

        var leftMeasures = FindMeasureMentions(leftText);
        var rightMeasures = FindMeasureMentions(rightText);
        var leftIds = leftMeasures.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToArray();
        var rightIds = rightMeasures.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToArray();
        if (leftIds.Length > 0 && rightIds.Length > 0)
        {
            return leftIds.SequenceEqual(rightIds, StringComparer.Ordinal);
        }

        // A single measure outside either category phrase is a shared
        // condition. A measure attached to just one side is a disjunction of
        // outcomes and requires clarification.
        var leftMeasureIsShared = leftMeasures.Any(item => item.Index + item.Length <= leftCategory.Index);
        var rightMeasureIsShared = rightMeasures.Any(item => item.Index >= rightCategory.Index + rightCategory.Length);
        return leftIds.Length == 0 && rightMeasureIsShared
            || rightIds.Length == 0 && leftMeasureIsShared;
    }

    internal static bool HasAmbiguousPlaceValue(string question)
    {
        // A date elsewhere in the question must not suppress an unqualified
        // place filter. Remove a trailing date clause before looking for a
        // place value, so "in North Block in May 2024" still asks which
        // dimension North Block belongs to.
        var placeQuestion = Regex.Replace(
            question,
            $@"\s+\b(?:in|during|for|on|noong|ngayong|sa)\s+(?:the\s+)?(?:(?:pm\s+)?cycle\s+)?(?:(?:calendar\s+)?year\s+)?(?:month\s+of\s+)?(?:(?:{MonthPattern})\s+)?\d{{4}}(?:\s+(?:pm\s+)?cycles?)?\s*[,.!?]?$",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);

        var place = Regex.Match(
            placeQuestion,
            @"\b(?:in|at|within|sa)\s+(?<value>""[^""]{1,128}""|'[^']{1,128}'|(?:[A-Z][\p{L}\p{N}&'/-]*|[A-Z0-9]{2,})(?:\s+(?:[A-Z][\p{L}\p{N}&'/-]*|[A-Z0-9]{2,}))*)\s*[,.!?]?\s*$",
            RegexOptions.CultureInvariant,
            RegexTimeout);
        if (!place.Success) return false;

        if (Regex.IsMatch(
                place.Groups["value"].Value.Trim().Trim('"', '\''),
                $@"^(?:{MonthPattern})$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexTimeout))
        {
            return false;
        }

        return !Regex.IsMatch(
            placeQuestion,
            @"\b(?:building|buildings|gusali|location|locations|lokasyon|department|departments|departamento|kagawaran|category|categories|cycle|cycles|pm)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            RegexTimeout);
    }

    private static bool HasMayDateContext(string question, int index, int length)
    {
        var before = question[..index];
        var after = question[(index + length)..];
        var yearImmediatelyBefore = Regex.IsMatch(before, @"\b\d{4}\s*$", RegexOptions.CultureInvariant, RegexTimeout);
        var yearImmediatelyAfter = Regex.IsMatch(after, @"^\s*,?\s*\d{4}\b", RegexOptions.CultureInvariant, RegexTimeout);
        var explicitDateLead = DateContext.IsMatch(before)
            || Regex.IsMatch(before, @"\b(?:pm\s+)?cycle\s+(?:in\s+)?$|\b(?:calendar\s+)?month\s+(?:of\s+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        var explicitDateFollow = Regex.IsMatch(after, @"^\s*(?:pm\s+cycle|cycle|calendar\s+month|month|inspections?|inspection|inspeksyon|inspeksiyon)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        return yearImmediatelyBefore || yearImmediatelyAfter || explicitDateLead || explicitDateFollow;
    }

    private static bool HasReportingContext(string question, int index, int length)
    {
        var start = Math.Max(0, index - 64);
        var end = Math.Min(question.Length, index + length + 64);
        var context = question[start..end];
        if (!ReportingContext.IsMatch(context))
        {
            return false;
        }

        if (Regex.IsMatch(question, @"(?<![\p{L}\p{N}])(?:alarm|alarms|alarm[\s-]+systems?|alarma\s+sa\s+sunog)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout)
            && Regex.IsMatch(question, @"\b(?:security|burglar|intrusion|security\s+alarm)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
        {
            return false;
        }

        return true;
    }

    internal static IReadOnlyList<string> ResolvePmGrouping(string question)
    {
        const string prefix = @"(?:by|per|group(?:ed)?\s+by|for\s+each|for\s+all|and|,|ayon\s+sa|bawat|kada|across|among)\s+(?:the\s+|mga\s+)?";
        const string ranked = @"(?:top|bottom|highest|lowest|(?<!at\s)most|(?<!at\s)least|rank(?:ed|ing)?|pinakamarami(?:ng)?|pinakakaunti|pinakamataas|pinakamababa)(?:\s+(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|lima|sampu))?\s+";
        var leadingLimit = $@"\b(?:which|alin(?:g)?)\s+(?:{NumberPattern})\s+";
        const string department = @"departments?|department|departamento|kagawaran";
        const string building = @"buildings?|building|gusali";
        const string category = @"categor(?:y|ies)|kategorya";
        var result = new List<string>();
        if (Regex.IsMatch(question, $@"\b(?:{prefix}|{ranked})(?:{department})\b|\bwhich\s+(?:{department})\b|{leadingLimit}(?:{department})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?(?:departamento|kagawaran)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.Department);
        if (Regex.IsMatch(question, $@"\b(?:{prefix}|{ranked})(?:{building})\b|\bwhich\s+(?:{building})\b|{leadingLimit}(?:{building})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?gusali\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.Building);
        if (Regex.IsMatch(question, $@"\b(?:{prefix}|{ranked})(?:{category})\b|{leadingLimit}(?:{category})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?(?:{category})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.AssetCategory);
        if (Regex.IsMatch(question, $@"\b(?:{prefix})(?:pm\s+)?(?:cycles?|siklo)\b|\bbawat\s+(?:pm\s+)?(?:cycle|siklo)\b|\bkada\s+(?:pm\s+)?(?:cycle|siklo)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.PmCycle);
        if (Regex.IsMatch(question, $@"\b(?:{prefix})(?:calendar\s+)?quarters?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.CalendarQuarter);
        if (Regex.IsMatch(question, $@"\b(?:{prefix})years?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.CalendarYear);
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<string> ResolveInventoryGrouping(string question)
    {
        const string prefix = @"(?:by|per|group(?:ed)?\s+by|for\s+each|for\s+all|across|among|ayon\s+sa|bawat|kada)\s+(?:the\s+|mga\s+)?";
        const string department = @"departments?|department|departamento|kagawaran";
        const string building = @"buildings?|building|gusali";
        const string category = @"categor(?:y|ies)|kategorya";
        var result = new List<string>();
        if (Regex.IsMatch(question, $@"\b{prefix}(?:{department})\b|\bwhich\s+(?:{department})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?(?:departamento|kagawaran)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.Department);
        if (Regex.IsMatch(question, $@"\b{prefix}(?:{building})\b|\bwhich\s+(?:{building})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?gusali\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.Building);
        if (Regex.IsMatch(question, $@"\b{prefix}(?:{category})\b|\bwhich\s+(?:{category})\b|\balin(?:g)?\s+(?:ang\s+)?(?:mga\s+)?(?:{category})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.AssetCategory);
        if (Regex.IsMatch(question, $@"\b{prefix}locations?\b|\bwhich\s+locations?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.Location);
        if (Regex.IsMatch(question, $@"\b{prefix}(?:registration\s+)?status\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout))
            result.Add(PmAnalyticsV3Catalog.RegistrationStatus);
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }
}
