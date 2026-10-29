using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniPM.Api.Features.Reports;

namespace UniPM.PmAnalytics.V3Evaluation;

internal static class ParaphraseCorpusValidation
{
    internal static int Run(string root)
    {
        var directory = Path.Combine(root, "reference/evaluation/pm-analytics-v3-paraphrases");
        var path = Path.Combine(directory, "cases.jsonl");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var bytes = File.ReadAllBytes(path);
        // Git's canonical corpus uses LF even in a Windows CRLF checkout.
        var canonical = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n"));
        var hash = Convert.ToHexStringLower(SHA256.HashData(canonical));
        var errors = new List<string>();
        if (hash != manifest.RootElement.GetProperty("sha256").GetString()) errors.Add("Corpus hash differs from manifest.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var supported = 0;
        var controls = 0;
        foreach (var line in File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement;
            var id = item.GetProperty("caseId").GetString()!;
            if (!ids.Add(id)) errors.Add($"{id}: duplicate ID.");
            var question = item.GetProperty("question").GetString()!;
            if (question.Length is 0 or > 512 || question.Any(char.IsControl)) errors.Add($"{id}: invalid question length or characters.");
            if (item.GetProperty("expectedStatus").GetString() != "Valid")
            {
                controls++;
                if (item.GetProperty("expectedPlan").ValueKind != JsonValueKind.Null) errors.Add($"{id}: control contains executable gold plan.");
                continue;
            }
            supported++;
            var plan = item.GetProperty("expectedPlan").Deserialize<PmAnalyticsV3ReportPlan>(options);
            if (!PmAnalyticsV3PlanValidator.TryNormalize(plan, out _, out var validationErrors))
                errors.Add($"{id}: {JsonSerializer.Serialize(validationErrors)}");
        }
        if (supported != 24 || controls != 6) errors.Add("Expected 24 supported questions and six controls.");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            corpusId = "V3P-001", sha256 = hash, supportedQuestions = supported, controls,
            valid = errors.Count == 0, errors, interpreterRuns = 0, providerCalls = 0,
            scope = "Structural and gold-plan validation only; label review is separate."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return errors.Count == 0 ? 0 : 1;
    }
}
