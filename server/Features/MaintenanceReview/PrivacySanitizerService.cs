using System.Text.RegularExpressions;

namespace UniPM.Api.Features.MaintenanceReview;

public sealed class PrivacySanitizerService
{
    public PrivacySanitizationSession CreateSession() => new();
}

public sealed class PrivacySanitizationSession
{
    private static readonly Regex EmailPattern = new(
        @"(?<![\w.+-])[\w.!#$%&'*+/=?^`{|}~-]+@[\w-]+(?:\.[\w-]+)+(?![\w.-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?:(?:\+63|0063)[\s-]*|0)9(?:[\s-]?\d){9}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LabeledIdPattern = new(
        @"\b(?:employee|student|staff|personnel)\s+(?:id|number|no\.?)\s*[:#-]?\s*[A-Z0-9][A-Z0-9-]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, string> tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> counters = new(StringComparer.Ordinal);

    public string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var sanitized = LabeledIdPattern.Replace(value, match => GetToken("EMPLOYEE_ID", match.Value));
        sanitized = EmailPattern.Replace(sanitized, match => GetToken("EMAIL", match.Value));
        return PhonePattern.Replace(sanitized, match => GetToken("PHONE", match.Value));
    }

    private string GetToken(string kind, string value)
    {
        var canonicalValue = kind == "PHONE"
            ? new string(value.Where(char.IsDigit).ToArray())
            : value.Trim();
        if (kind == "PHONE" && canonicalValue.StartsWith("63", StringComparison.Ordinal))
        {
            canonicalValue = $"0{canonicalValue[2..]}";
        }

        var key = $"{kind}:{canonicalValue}";
        if (tokens.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var number = counters.TryGetValue(kind, out var current) ? current + 1 : 1;
        counters[kind] = number;
        var token = $"[{kind}_{number}]";
        tokens[key] = token;
        return token;
    }
}
