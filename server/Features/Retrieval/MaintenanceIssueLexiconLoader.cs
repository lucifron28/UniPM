using System.Text.Json;
using System.Text.Json.Serialization;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.Api.Features.Retrieval;

public sealed class MaintenanceIssueLexiconLoader(MaintenanceIssueLexiconOptions options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public MaintenanceIssueLexiconDocument Load()
    {
        if (!File.Exists(options.LexiconPath))
        {
            throw new FileNotFoundException(
                "The maintenance issue lexicon was not found in the application resources.",
                options.LexiconPath);
        }

        MaintenanceIssueLexiconDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<MaintenanceIssueLexiconDocument>(
                File.ReadAllText(options.LexiconPath),
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new MaintenanceIssueLexiconException(
                "The maintenance issue lexicon contains invalid or unmapped JSON properties.",
                exception);
        }

        if (document is null)
        {
            throw new MaintenanceIssueLexiconException("The maintenance issue lexicon is empty.");
        }

        Validate(document);
        return document;
    }

    private static void Validate(MaintenanceIssueLexiconDocument document)
    {
        var errors = new List<string>();

        if (!string.Equals(
                document.Schema,
                MaintenanceIssueLexiconOptions.SchemaFileName,
                StringComparison.Ordinal))
        {
            errors.Add("The lexicon schema reference is invalid.");
        }

        if (!string.Equals(
                document.LexiconVersion,
                MaintenanceIssueLexiconOptions.SupportedLexiconVersion,
                StringComparison.Ordinal))
        {
            errors.Add($"Unsupported lexicon version '{document.LexiconVersion}'.");
        }

        AddDuplicateError(document.Issues.Select(issue => issue.Key), "issue keys", errors);

        var suppliedKeys = document.Issues.Select(issue => issue.Key).ToHashSet(StringComparer.Ordinal);
        if (!MaintenanceIssueLexiconOptions.SupportedIssueKeys.SetEquals(suppliedKeys))
        {
            errors.Add("The lexicon must contain exactly the approved v1.0 issue keys.");
        }

        var aliases = new HashSet<string>(StringComparer.Ordinal);

        foreach (var issue in document.Issues)
        {
            if (string.IsNullOrWhiteSpace(issue.Key)
                || !MaintenanceIssueLexiconOptions.SupportedIssueKeys.Contains(issue.Key))
            {
                errors.Add("The lexicon contains an unsupported or empty issue key.");
            }

            if (!AssetCategoryCatalog.ContainsCode(issue.AssetCategory))
            {
                errors.Add($"Issue '{issue.Key}' uses an unsupported asset category.");
            }

            if (issue.Aliases.Count == 0)
            {
                errors.Add($"Issue '{issue.Key}' must contain at least one alias.");
                continue;
            }

            foreach (var alias in issue.Aliases)
            {
                var normalizedAlias = MaintenanceIssueText.Normalize(alias);
                if (string.IsNullOrWhiteSpace(normalizedAlias))
                {
                    errors.Add($"Issue '{issue.Key}' contains an empty alias.");
                }
                else if (!aliases.Add(normalizedAlias))
                {
                    errors.Add($"The lexicon contains a duplicate alias '{alias}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new MaintenanceIssueLexiconException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void AddDuplicateError(
        IEnumerable<string> values,
        string label,
        List<string> errors)
    {
        if (values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            errors.Add($"The lexicon contains duplicate {label}.");
        }
    }
}

public sealed class MaintenanceIssueLexiconException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
