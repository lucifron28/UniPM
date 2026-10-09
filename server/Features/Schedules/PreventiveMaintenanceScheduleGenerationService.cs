using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

public sealed record ScheduleGenerationResult(
    int Year,
    int EligibleAssets,
    int ExistingSchedules,
    int CreatedSchedules);

public sealed class PreventiveMaintenanceScheduleGenerationService(
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public const string UniqueIndexName = "UX_Schedules_AssetId_PmCycle";
    private const int MaxUniqueConflictRetries = 3;

    public async Task<int> AddCurrentYearUpcomingCyclesAsync(
        ApplicationDbContext context,
        Asset asset,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!IsEligible(asset))
        {
            return 0;
        }

        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        var months = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
        var existingCycles = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Where(schedule => schedule.AssetId == asset.Id)
            .Select(schedule => schedule.PmCycle)
            .ToListAsync(cancellationToken);
        var existing = existingCycles.ToHashSet(StringComparer.Ordinal);
        var missing = BuildMissingSchedules(asset, institutionalNow.Year, institutionalNow.Month, now, months, existing);

        context.PreventiveMaintenanceSchedules.AddRange(missing);
        return missing.Count;
    }

    public async Task<ScheduleGenerationResult> EnsureYearAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        if (year is < 2000 || year > institutionalNow.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Schedule generation supports years from 2000 through the current institutional year.");
        }

        for (var attempt = 0; attempt < MaxUniqueConflictRetries; attempt++)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            try
            {
                return await EnsureYearOnceAsync(context, year, now, cancellationToken);
            }
            catch (DbUpdateException exception)
                when (DatabaseConstraintViolation.IsUniqueConstraint(exception, UniqueIndexName)
                    && attempt + 1 < MaxUniqueConflictRetries)
            {
                // A concurrent run inserted one or more cycles. The next fresh context reloads
                // the authoritative set and retries only the cycles that remain missing.
            }
        }

        throw new InvalidOperationException("Schedule generation could not settle after concurrent inserts.");
    }

    private static async Task<ScheduleGenerationResult> EnsureYearOnceAsync(
        ApplicationDbContext context,
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var assets = await context.Assets
            .AsNoTracking()
            .Where(asset => asset.Status == AssetStatusCatalog.Active
                && asset.Department != null
                && asset.Department.Trim() != string.Empty)
            .ToListAsync(cancellationToken);

        var eligible = assets
            .Where(IsEligible)
            .Where(asset => PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt).Year <= year)
            .ToArray();
        if (eligible.Length == 0)
        {
            return new ScheduleGenerationResult(year, 0, 0, 0);
        }

        var assetIds = eligible.Select(asset => asset.Id).ToArray();
        var existingCycles = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Where(schedule => assetIds.Contains(schedule.AssetId)
                && schedule.PmCycle.StartsWith(year.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Select(schedule => new { schedule.AssetId, schedule.PmCycle })
            .ToListAsync(cancellationToken);
        var generated = new List<PreventiveMaintenanceSchedule>();
        var expectedCycleCount = 0;
        foreach (var asset in eligible)
        {
            var months = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
            var assetCreatedAt = PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt);
            var alreadyPresent = existingCycles
                .Where(schedule => schedule.AssetId == asset.Id)
                .Select(schedule => schedule.PmCycle)
                .ToHashSet(StringComparer.Ordinal);
            expectedCycleCount += months.Count(month => assetCreatedAt.Year < year || month >= assetCreatedAt.Month);
            generated.AddRange(BuildMissingSchedules(asset, year, 1, now, months, alreadyPresent));
        }

        context.PreventiveMaintenanceSchedules.AddRange(generated);
        if (generated.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new ScheduleGenerationResult(
            year,
            eligible.Length,
            Math.Max(0, expectedCycleCount - generated.Count),
            generated.Count);
    }

    private static List<PreventiveMaintenanceSchedule> BuildMissingSchedules(
        Asset asset,
        int year,
        int firstMonth,
        DateTimeOffset now,
        IReadOnlyList<int> months,
        IReadOnlySet<string> existingCycles)
    {
        var assetCreatedAt = PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt);
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        var result = new List<PreventiveMaintenanceSchedule>();

        foreach (var month in months)
        {
            if (month < firstMonth || assetCreatedAt.Year > year
                || (assetCreatedAt.Year == year && month < assetCreatedAt.Month))
            {
                continue;
            }

            var pmCycle = $"{year:D4}-{month:D2}";
            if (existingCycles.Contains(pmCycle))
            {
                continue;
            }

            var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle);
            var isQuarterly = months.Count == 4;
            var quarter = $"Q{((month - 1) / 3) + 1}";
            result.Add(new PreventiveMaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                ScheduleDate = deadline,
                PmCycle = pmCycle,
                PeriodType = isQuarterly ? SchedulePeriodTypeCatalog.Quarter : SchedulePeriodTypeCatalog.Semester,
                Quarter = isQuarterly ? quarter : null,
                // Semester labels remain unset because current PM cycles do not define academic terms.
                Semester = null,
                Year = year,
                Status = deadline < institutionalNow ? ScheduleStatusCatalog.Overdue : ScheduleStatusCatalog.Due,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return result;
    }

    private static bool IsEligible(Asset asset)
    {
        return asset.Status == AssetStatusCatalog.Active
            && !string.IsNullOrWhiteSpace(asset.Department)
            && CpmpScheduleFrequency.GetMonths(asset.AssetCategory).Count > 0;
    }
}
