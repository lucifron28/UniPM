using System.Text.Json;
using System.Text.Json.Serialization;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.ReferenceData;

namespace UniPM.RetrievalBenchmark;

public sealed class RetrievalEvaluationManifestLoader(
    SyntheticMaintenanceDataset operationalDataset)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<RetrievalEvaluationManifest> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("The retrieval evaluation manifest was not found.", manifestPath);
        }

        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<RetrievalEvaluationManifest>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (manifest is null)
            {
                throw new RetrievalEvaluationManifestException("The retrieval evaluation manifest is empty.");
            }

            Validate(manifest);
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new RetrievalEvaluationManifestException(
                "The retrieval evaluation manifest contains invalid or unmapped JSON properties.",
                exception);
        }
    }

    private void Validate(RetrievalEvaluationManifest manifest)
    {
        var errors = new List<string>();
        var assetsById = operationalDataset.Assets.ToDictionary(asset => asset.Id);
        var inspectionsById = operationalDataset.Inspections.ToDictionary(inspection => inspection.Id);
        var coldStartAssetIds = operationalDataset.Assets
            .Where(asset => operationalDataset.Schedules.Any(
                schedule => schedule.AssetId == asset.Id && string.Equals(schedule.Status, "Due", StringComparison.Ordinal)))
            .Where(asset => operationalDataset.Inspections.All(inspection => inspection.AssetId != asset.Id))
            .Select(asset => asset.Id)
            .ToHashSet();

        if (!string.Equals(manifest.Schema, RetrievalBenchmarkVocabulary.SchemaFileName, StringComparison.Ordinal))
        {
            errors.Add("The evaluation manifest schema reference is invalid.");
        }

        if (!string.Equals(manifest.EvaluationVersion, RetrievalBenchmarkVocabulary.EvaluationVersion, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported evaluation version '{manifest.EvaluationVersion}'.");
        }

        if (!string.Equals(manifest.DatasetVersion, RetrievalBenchmarkVocabulary.DatasetVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.DatasetVersion, operationalDataset.DatasetVersion, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported or mismatched dataset version '{manifest.DatasetVersion}'.");
        }

        ValidateAssetAnnotations(manifest, assetsById, coldStartAssetIds, errors);
        ValidateRecordAnnotations(manifest, inspectionsById, errors);
        ValidateQueries(manifest, assetsById, inspectionsById, coldStartAssetIds, errors);

        if (errors.Count > 0)
        {
            throw new RetrievalEvaluationManifestException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateAssetAnnotations(
        RetrievalEvaluationManifest manifest,
        IReadOnlyDictionary<Guid, SyntheticAsset> assetsById,
        IReadOnlySet<Guid> coldStartAssetIds,
        List<string> errors)
    {
        AddDuplicateError(manifest.AssetAnnotations.Select(annotation => annotation.AssetId), "asset annotation IDs", errors);
        AddDuplicateError(manifest.AssetAnnotations.Select(annotation => annotation.AssetCode), "asset annotation codes", errors);

        if (manifest.AssetAnnotations.Count != coldStartAssetIds.Count)
        {
            errors.Add("The evaluation manifest must annotate every cold-start asset exactly once.");
        }

        foreach (var annotation in manifest.AssetAnnotations)
        {
            if (!assetsById.TryGetValue(annotation.AssetId, out var asset))
            {
                errors.Add($"Asset annotation '{annotation.AssetId}' references an unknown asset.");
                continue;
            }

            if (!string.Equals(annotation.AssetCode, asset.AssetCode, StringComparison.Ordinal))
            {
                errors.Add($"Asset annotation '{annotation.AssetId}' has a mismatched asset code.");
            }

            if (!coldStartAssetIds.Contains(annotation.AssetId)
                || annotation.Tags.Count != 1
                || !string.Equals(annotation.Tags[0], "cold-start", StringComparison.Ordinal))
            {
                errors.Add($"Asset annotation '{annotation.AssetId}' must identify a cold-start asset.");
            }
        }

        if (!coldStartAssetIds.SetEquals(manifest.AssetAnnotations.Select(annotation => annotation.AssetId)))
        {
            errors.Add("Cold-start asset annotations do not match the operational dataset.");
        }
    }

    private static void ValidateRecordAnnotations(
        RetrievalEvaluationManifest manifest,
        IReadOnlyDictionary<Guid, SyntheticInspection> inspectionsById,
        List<string> errors)
    {
        AddDuplicateError(manifest.RecordAnnotations.Select(annotation => annotation.InspectionId), "record annotation IDs", errors);
        AddDuplicateError(manifest.RecordAnnotations.Select(annotation => annotation.InspectionSeedKey), "record annotation seed keys", errors);

        if (manifest.RecordAnnotations.Count != inspectionsById.Count)
        {
            errors.Add("The evaluation manifest must annotate every operational inspection exactly once.");
        }

        foreach (var annotation in manifest.RecordAnnotations)
        {
            if (!inspectionsById.TryGetValue(annotation.InspectionId, out var inspection))
            {
                errors.Add($"Record annotation '{annotation.InspectionId}' references an unknown inspection.");
                continue;
            }

            if (!string.Equals(annotation.InspectionSeedKey, inspection.SeedKey, StringComparison.Ordinal))
            {
                errors.Add($"Record annotation '{annotation.InspectionId}' has a mismatched seed key.");
            }
        }

        if (!inspectionsById.Keys.ToHashSet().SetEquals(manifest.RecordAnnotations.Select(annotation => annotation.InspectionId)))
        {
            errors.Add("Record annotations do not match the operational inspection IDs.");
        }
    }

    private static void ValidateQueries(
        RetrievalEvaluationManifest manifest,
        IReadOnlyDictionary<Guid, SyntheticAsset> assetsById,
        IReadOnlyDictionary<Guid, SyntheticInspection> inspectionsById,
        IReadOnlySet<Guid> coldStartAssetIds,
        List<string> errors)
    {
        if (manifest.Queries.Count is < 20 or > 24)
        {
            errors.Add("The benchmark manifest must contain between 20 and 24 queries.");
        }

        AddDuplicateError(manifest.Queries.Select(query => query.QueryId), "query IDs", errors);
        var languages = new HashSet<string>(StringComparer.Ordinal);
        var categories = new HashSet<string>(StringComparer.Ordinal);
        var coverageTags = new HashSet<string>(StringComparer.Ordinal);
        var annotationsById = manifest.RecordAnnotations.ToDictionary(annotation => annotation.InspectionId);

        foreach (var query in manifest.Queries)
        {
            if (string.IsNullOrWhiteSpace(query.QueryId)
                || !query.QueryId.StartsWith('Q')
                || query.QueryId.Length != 4
                || !int.TryParse(query.QueryId.AsSpan(1), out _))
            {
                errors.Add($"Query '{query.QueryId}' has an invalid query ID.");
            }

            if (string.IsNullOrWhiteSpace(query.QueryText) || query.QueryText.Length > 256)
            {
                errors.Add($"Query '{query.QueryId}' must contain bounded query text.");
            }

            foreach (var scenarioTag in RetrievalBenchmarkVocabulary.QueryScenarioTags)
            {
                if (query.QueryText.Contains(scenarioTag, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Query '{query.QueryId}' must not include scenario metadata '{scenarioTag}' in QueryText.");
                }
            }

            if (!RetrievalBenchmarkVocabulary.Languages.Contains(query.Language))
            {
                errors.Add($"Query '{query.QueryId}' uses unsupported language '{query.Language}'.");
            }
            else
            {
                languages.Add(query.Language);
            }

            if (!AssetCategoryCatalog.ContainsCode(query.AssetCategory))
            {
                errors.Add($"Query '{query.QueryId}' uses unsupported category '{query.AssetCategory}'.");
            }
            else
            {
                categories.Add(query.AssetCategory);
            }

            foreach (var tag in query.ScenarioTags)
            {
                if (!RetrievalBenchmarkVocabulary.QueryScenarioTags.Contains(tag))
                {
                    errors.Add($"Query '{query.QueryId}' uses unsupported scenario tag '{tag}'.");
                }
                else
                {
                    coverageTags.Add(tag);
                }
            }

            ValidateFilters(query, assetsById, errors);

            if (query.ContextAssetId is not null)
            {
                if (!assetsById.ContainsKey(query.ContextAssetId.Value))
                {
                    errors.Add($"Query '{query.QueryId}' references an unknown context asset.");
                }
                else if (query.ScenarioTags.Contains("cold-start", StringComparer.Ordinal)
                    && !coldStartAssetIds.Contains(query.ContextAssetId.Value))
                {
                    errors.Add($"Cold-start query '{query.QueryId}' must reference an annotated cold-start asset.");
                }
            }

            if (query.ScenarioTags.Contains("cold-start", StringComparer.Ordinal)
                && (query.ContextAssetId is null || query.RetrievalFilters.AssetId is not null))
            {
                errors.Add($"Cold-start query '{query.QueryId}' must use ContextAssetId without an AssetId retrieval filter.");
            }

            AddDuplicateError(
                query.ExpectedRelevantInspectionIds,
                $"expected inspection IDs for query '{query.QueryId}'",
                errors);

            if (query.ExpectedRelevantInspectionIds.Count == 0)
            {
                errors.Add($"Query '{query.QueryId}' must contain at least one expected inspection.");
            }

            foreach (var inspectionId in query.ExpectedRelevantInspectionIds)
            {
                if (!inspectionsById.TryGetValue(inspectionId, out var inspection))
                {
                    errors.Add($"Query '{query.QueryId}' references unknown inspection '{inspectionId}'.");
                    continue;
                }

                if (!string.Equals(inspection.AssetCategory, query.AssetCategory, StringComparison.Ordinal))
                {
                    errors.Add($"Query '{query.QueryId}' expects an inspection outside its category filter.");
                }

                if (assetsById.TryGetValue(inspection.AssetId, out var asset))
                {
                    var filters = query.RetrievalFilters;
                    if (filters.AssetId is not null && filters.AssetId != inspection.AssetId)
                    {
                        errors.Add($"Query '{query.QueryId}' expects an inspection outside its AssetId filter.");
                    }

                    if (!MatchesOptionalFilter(filters.AssetCategory, inspection.AssetCategory)
                        || !MatchesOptionalFilter(filters.Building, asset.Building)
                        || !MatchesOptionalFilter(filters.Department, asset.Department)
                        || !MatchesOptionalFilter(filters.Location, asset.Location)
                        || (filters.IsOperational is not null && filters.IsOperational != inspection.IsOperational)
                        || (filters.DateFrom is not null && inspection.DateInspected < filters.DateFrom.Value)
                        || (filters.DateTo is not null && inspection.DateInspected > filters.DateTo.Value))
                    {
                        errors.Add($"Query '{query.QueryId}' expects an inspection outside one of its retrieval filters.");
                    }
                }

                if (annotationsById.TryGetValue(inspectionId, out var annotation)
                    && annotation.ScenarioTags.Any(tag => string.Equals(tag, "distractor", StringComparison.Ordinal)
                        || string.Equals(tag, "different-category-distractor", StringComparison.Ordinal)))
                {
                    errors.Add($"Query '{query.QueryId}' expects an inspection marked only as a distractor.");
                }

                if (query.ScenarioTags.Contains("same-asset-history", StringComparer.Ordinal)
                    && query.RetrievalFilters.AssetId != inspection.AssetId)
                {
                    errors.Add($"Same-asset query '{query.QueryId}' expects an inspection outside its AssetId filter.");
                }
            }
        }

        if (!RetrievalBenchmarkVocabulary.Languages.SetEquals(languages))
        {
            errors.Add("Benchmark queries must cover English, Tagalog, and Taglish.");
        }

        if (categories.Count != 4)
        {
            errors.Add("Benchmark queries must cover all four supported asset categories.");
        }

        foreach (var tag in RetrievalBenchmarkVocabulary.RequiredCoverageTags)
        {
            if (!coverageTags.Contains(tag))
            {
                errors.Add($"Benchmark queries must include scenario coverage for '{tag}'.");
            }
        }
    }

    private static void ValidateFilters(
        RetrievalEvaluationQuery query,
        IReadOnlyDictionary<Guid, SyntheticAsset> assetsById,
        List<string> errors)
    {
        var filters = query.RetrievalFilters;
        if (filters.AssetId is not null && !assetsById.ContainsKey(filters.AssetId.Value))
        {
            errors.Add($"Query '{query.QueryId}' uses an unknown AssetId filter.");
        }

        if (filters.AssetCategory is not null)
        {
            if (!AssetCategoryCatalog.ContainsCode(filters.AssetCategory))
            {
                errors.Add($"Query '{query.QueryId}' uses an unsupported AssetCategory filter.");
            }
            else if (!string.Equals(filters.AssetCategory, query.AssetCategory, StringComparison.Ordinal))
            {
                errors.Add($"Query '{query.QueryId}' has a category filter that does not match its category.");
            }
        }

        foreach (var (name, value) in new[]
        {
            ("Building", filters.Building),
            ("Department", filters.Department),
            ("Location", filters.Location)
        })
        {
            if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > 256))
            {
                errors.Add($"Query '{query.QueryId}' has an invalid {name} filter.");
            }
        }

        if (filters.DateFrom is not null && filters.DateTo is not null && filters.DateFrom > filters.DateTo)
        {
            errors.Add($"Query '{query.QueryId}' has a reversed date range.");
        }
    }

    private static void AddDuplicateError<T>(IEnumerable<T> values, string label, List<string> errors)
        where T : notnull
    {
        if (values.GroupBy(value => value).Any(group => group.Count() > 1))
        {
            errors.Add($"The evaluation manifest contains duplicate {label}.");
        }
    }

    private static bool MatchesOptionalFilter(string? filter, string? value)
        => filter is null || string.Equals(filter, value, StringComparison.Ordinal);
}
