using System.Text.Json;
using System.Text.RegularExpressions;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticMaintenanceDatasetValidator
{
    private static readonly Regex EmailPattern = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PhilippineMobilePattern = new(
        @"(?<!\d)(?:\+63|0)9\d{2}[-\s]?\d{3}[-\s]?\d{4}(?!\d)",
        RegexOptions.CultureInvariant);

    private static readonly Regex InstitutionalIdPattern = new(
        @"\b(?:employee|student|staff)\s*id\s*(?:[:#-]\s*)?[A-Z0-9-]*\d[A-Z0-9-]*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Validate(SyntheticMaintenanceDataset dataset)
    {
        var errors = new List<string>();

        if (!string.Equals(
                dataset.DatasetVersion,
                SyntheticMaintenanceSeedOptions.SupportedDatasetVersion,
                StringComparison.Ordinal))
        {
            errors.Add($"Unsupported dataset version '{dataset.DatasetVersion}'.");
        }

        ValidateExpectedCounts(dataset, errors);
        ValidateUniqueness(dataset, errors);

        var assetsById = dataset.Assets
            .GroupBy(asset => asset.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var actorsById = dataset.Actors
            .GroupBy(actor => actor.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var schedulesById = dataset.Schedules
            .GroupBy(schedule => schedule.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var inspectionsByScheduleId = dataset.Inspections
            .GroupBy(inspection => inspection.ScheduleId)
            .ToDictionary(group => group.Key, group => group.ToList());

        if (!AssetCategoryCatalog.PersistedCodes.SetEquals(dataset.Assets.Select(asset => asset.AssetCategory)))
        {
            errors.Add("The fixture must contain all four supported asset categories.");
        }

        foreach (var actor in dataset.Actors)
        {
            if (actor.Id == Guid.Empty)
            {
                errors.Add($"Actor '{actor.SeedKey}' has an empty ID.");
            }

            if (!SyntheticActorRoleCatalog.SeedOnlyCodes.Contains(actor.RoleToken))
            {
                errors.Add($"Actor '{actor.SeedKey}' uses an unsupported role token.");
            }

            if (!actor.DisplayLabel.StartsWith("Synthetic", StringComparison.Ordinal))
            {
                errors.Add($"Actor '{actor.SeedKey}' must use a synthetic display label.");
            }
        }

        foreach (var asset in dataset.Assets)
        {
            if (asset.Id == Guid.Empty)
            {
                errors.Add($"Asset '{asset.SeedKey}' has an empty ID.");
            }

            if (!AssetCategoryCatalog.TryNormalize(asset.AssetCategory, out var normalizedCategory))
            {
                errors.Add($"Asset '{asset.SeedKey}' uses an unsupported asset category.");
            }
            else if (!string.Equals(asset.AssetCategory, normalizedCategory, StringComparison.Ordinal))
            {
                errors.Add($"Asset '{asset.SeedKey}' asset category is not canonical.");
            }

            if (!AssetStatusCatalog.TryNormalize(asset.Status, out var normalizedStatus))
            {
                errors.Add($"Asset '{asset.SeedKey}' uses an unsupported status.");
            }
            else if (!string.Equals(asset.Status, normalizedStatus, StringComparison.Ordinal))
            {
                errors.Add($"Asset '{asset.SeedKey}' status is not canonical.");
            }

            if (string.IsNullOrWhiteSpace(asset.AssetCode)
                || asset.AssetCode.Length > AssetCodeValue.MaxLength)
            {
                errors.Add($"Asset '{asset.SeedKey}' has an invalid asset code length.");
            }
            else if (!string.Equals(asset.AssetCode, AssetCodeValue.Normalize(asset.AssetCode), StringComparison.Ordinal))
            {
                errors.Add($"Asset '{asset.SeedKey}' asset code is not canonical.");
            }

            var expectedQrCodeValue = AssetQrCodeValue.Create(asset.AssetCategory, asset.Id);
            if (!string.Equals(asset.QrCodeValue, expectedQrCodeValue, StringComparison.Ordinal))
            {
                errors.Add($"Asset '{asset.SeedKey}' has a QR value that does not match the shared QR format.");
            }
        }

        foreach (var schedule in dataset.Schedules)
        {
            if (schedule.Id == Guid.Empty)
            {
                errors.Add($"Schedule '{schedule.SeedKey}' has an empty ID.");
            }

            if (!SchedulePeriodTypeCatalog.TryNormalize(schedule.PeriodType, out var normalizedPeriodType))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' uses an unsupported period type.");
            }
            else if (!string.Equals(schedule.PeriodType, normalizedPeriodType, StringComparison.Ordinal))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' period type is not canonical.");
            }

            if (!ScheduleQuarterCatalog.TryNormalizeNullable(schedule.Quarter, out var normalizedQuarter))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' uses an unsupported quarter.");
            }
            else if (!string.Equals(schedule.Quarter, normalizedQuarter, StringComparison.Ordinal))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' quarter is not canonical.");
            }

            if (schedule.Semester is not null
                && !ScheduleSemesterCatalog.PersistedCodes.Contains(schedule.Semester))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' uses an unsupported semester.");
            }

            if (!ScheduleStatusCatalog.TryNormalize(schedule.Status, out var normalizedScheduleStatus))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' uses an unsupported status.");
            }
            else if (!string.Equals(schedule.Status, normalizedScheduleStatus, StringComparison.Ordinal))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' status is not canonical.");
            }

            if (!assetsById.TryGetValue(schedule.AssetId, out var asset))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' references an unknown asset.");
            }
            else if (!string.Equals(schedule.AssetCode, asset.AssetCode, StringComparison.Ordinal))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' asset code does not match its asset.");
            }

            if (!actorsById.ContainsKey(schedule.AssignedToUserId))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' references an unknown assignee.");
            }

            if (schedule.Status is not (ScheduleStatusCatalog.Completed or ScheduleStatusCatalog.Due))
            {
                errors.Add($"Schedule '{schedule.SeedKey}' has unsupported status '{schedule.Status}'.");
            }

            if (schedule.Status == ScheduleStatusCatalog.Completed && schedule.CompletedAt is null)
            {
                errors.Add($"Completed schedule '{schedule.SeedKey}' must have a completion timestamp.");
            }

            if (schedule.Status == ScheduleStatusCatalog.Due && schedule.CompletedAt is not null)
            {
                errors.Add($"Due schedule '{schedule.SeedKey}' must not have a completion timestamp.");
            }
        }

        foreach (var inspection in dataset.Inspections)
        {
            if (inspection.Id == Guid.Empty)
            {
                errors.Add($"Inspection '{inspection.SeedKey}' has an empty ID.");
            }

            if (!schedulesById.TryGetValue(inspection.ScheduleId, out var schedule))
            {
                errors.Add($"Inspection '{inspection.SeedKey}' references an unknown schedule.");
                continue;
            }

            if (!assetsById.TryGetValue(inspection.AssetId, out var asset))
            {
                errors.Add($"Inspection '{inspection.SeedKey}' references an unknown asset.");
            }
            else
            {
                if (inspection.AssetId != schedule.AssetId)
                {
                    errors.Add($"Inspection '{inspection.SeedKey}' asset ID does not match its schedule asset ID.");
                }

                if (!string.Equals(inspection.AssetCode, asset.AssetCode, StringComparison.Ordinal)
                    || !string.Equals(inspection.AssetCategory, asset.AssetCategory, StringComparison.Ordinal))
                {
                    errors.Add($"Inspection '{inspection.SeedKey}' does not match its asset identity.");
                }
            }

            if (!actorsById.ContainsKey(inspection.InspectorUserId))
            {
                errors.Add($"Inspection '{inspection.SeedKey}' references an unknown inspector.");
            }
        }

        foreach (var schedule in dataset.Schedules)
        {
            var inspectionCount = inspectionsByScheduleId.TryGetValue(schedule.Id, out var inspections)
                ? inspections.Count
                : 0;

            if (schedule.Status == ScheduleStatusCatalog.Completed && inspectionCount != 1)
            {
                errors.Add($"Completed schedule '{schedule.SeedKey}' must have exactly one inspection.");
            }

            if (schedule.Status == ScheduleStatusCatalog.Due && inspectionCount != 0)
            {
                errors.Add($"Due schedule '{schedule.SeedKey}' must not have an inspection.");
            }
        }

        ValidateSensitiveContent(dataset, errors);

        if (errors.Count > 0)
        {
            throw new SyntheticMaintenanceFixtureException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateExpectedCounts(SyntheticMaintenanceDataset dataset, List<string> errors)
    {
        if (dataset.ExpectedCounts.Actors != dataset.Actors.Count
            || dataset.ExpectedCounts.Assets != dataset.Assets.Count
            || dataset.ExpectedCounts.Schedules != dataset.Schedules.Count
            || dataset.ExpectedCounts.Inspections != dataset.Inspections.Count)
        {
            errors.Add("Fixture expected counts do not match the supplied records.");
        }
    }

    private static void ValidateUniqueness(SyntheticMaintenanceDataset dataset, List<string> errors)
    {
        AddDuplicateError(dataset.Actors.Select(actor => actor.Id), "actor IDs", errors);
        AddDuplicateError(dataset.Assets.Select(asset => asset.Id), "asset IDs", errors);
        AddDuplicateError(dataset.Schedules.Select(schedule => schedule.Id), "schedule IDs", errors);
        AddDuplicateError(dataset.Inspections.Select(inspection => inspection.Id), "inspection IDs", errors);
        AddDuplicateError(
            dataset.Assets.Select(asset => AssetCodeValue.Normalize(asset.AssetCode)),
            "asset codes",
            errors);
        AddDuplicateError(
            dataset.Assets.Select(asset => AssetCodeValue.NormalizeQrCode(asset.QrCodeValue)),
            "QR values",
            errors);
        AddDuplicateError(
            dataset.Actors.Select(actor => actor.SeedKey)
                .Concat(dataset.Assets.Select(asset => asset.SeedKey))
                .Concat(dataset.Schedules.Select(schedule => schedule.SeedKey))
                .Concat(dataset.Inspections.Select(inspection => inspection.SeedKey)),
            "seed keys",
            errors,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AddDuplicateError<T>(
        IEnumerable<T> values,
        string label,
        List<string> errors,
        IEqualityComparer<T>? comparer = null)
        where T : notnull
    {
        if (values.GroupBy(value => value, comparer ?? EqualityComparer<T>.Default).Any(group => group.Count() > 1))
        {
            errors.Add($"Fixture contains duplicate {label}.");
        }
    }

    private static void ValidateSensitiveContent(SyntheticMaintenanceDataset dataset, List<string> errors)
    {
        var serializedDataset = JsonSerializer.Serialize(dataset);

        if (EmailPattern.IsMatch(serializedDataset))
        {
            errors.Add("Fixture contains an email-like value.");
        }

        if (PhilippineMobilePattern.IsMatch(serializedDataset))
        {
            errors.Add("Fixture contains a Philippine mobile-like value.");
        }

        if (InstitutionalIdPattern.IsMatch(serializedDataset))
        {
            errors.Add("Fixture contains an employee, student, or staff ID-like value.");
        }

    }
}

public sealed class SyntheticMaintenanceFixtureException(string message) : InvalidOperationException(message);
