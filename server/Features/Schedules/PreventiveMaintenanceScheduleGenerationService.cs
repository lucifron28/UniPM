using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using UniPM.Api.Data;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Schedules;

public sealed record ScheduleGenerationResult(
    int Year,
    int EligibleAssets,
    int ExistingSchedules,
    int CreatedSchedules,
    int DeferredSchedules,
    int CyclesRequiringGsdCoverageReview);

public sealed record ScheduleCoverageReviewItem(
    Guid AssetId,
    string AssetCode,
    string Department,
    string AssetCategory,
    string PmCycle,
    string Reason);

public sealed class PreventiveMaintenanceScheduleGenerationService
{
    public const string UniqueIndexName = "UX_Schedules_AssetId_PmCycle";
    private const int MaxUniqueConflictRetries = 3;
    private const int PastCycleLookupBatchSize = 500;
    private readonly IDbContextFactory<ApplicationDbContext> contextFactory;
    private readonly DateOnly? effectiveDate;

    public PreventiveMaintenanceScheduleGenerationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<ScheduleGenerationOptions>? options = null)
    {
        this.contextFactory = contextFactory;
        var configuredDate = options?.Value.EffectiveDate;
        if (!string.IsNullOrWhiteSpace(configuredDate))
        {
            if (!DateOnly.TryParseExact(
                    configuredDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                throw new InvalidOperationException(
                    "ScheduleGeneration:EffectiveDate must use yyyy-MM-dd and requires GSD approval.");
            }

            effectiveDate = parsedDate;
        }
    }

    public async Task<int> AddCurrentYearUpcomingCyclesAsync(
        ApplicationDbContext context,
        Asset asset,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null)
    {
        if (!IsEligible(asset))
        {
            return 0;
        }

        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        var months = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
        var assetCreated = PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt);
        var cycles = months
            .Where(month => month >= Math.Max(
                institutionalNow.Month,
                assetCreated.Year == institutionalNow.Year ? assetCreated.Month : 1))
            .Select(month => $"{institutionalNow.Year:D4}-{month:D2}")
            .Where(IsWithinApprovedCoverage)
            .ToArray();
        var identities = cycles
            .Select(cycle => ScheduleBatchIdentity.TryCreate(
                asset.Department, asset.AssetCategory, cycle, out var identity) ? identity : (ScheduleBatchIdentity?)null)
            .Where(identity => identity is not null)
            .Select(identity => identity!.Value)
            .OrderBy(identity => identity.Department, StringComparer.Ordinal)
            .ThenBy(identity => identity.AssetCategory, StringComparer.Ordinal)
            .ThenBy(identity => identity.PmCycle, StringComparer.Ordinal)
            .ToArray();

        if (transaction is null
            && string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Asset enrollment must hold one transaction for its batch locks.");
        }

        var leases = new List<ScheduleBatchMutationLockLease>(identities.Length);
        try
        {
            foreach (var identity in identities)
            {
                var lease = await ScheduleBatchMutationLockLease.AcquireAsync(
                    context, identity, cancellationToken, transaction);
                if (!lease.Acquired)
                {
                    throw new ScheduleBatchMutationConflictException();
                }

                leases.Add(lease);
            }

            var existingCycles = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => schedule.AssetId == asset.Id)
                .Select(schedule => schedule.PmCycle)
                .ToListAsync(cancellationToken);
            var existing = existingCycles.ToHashSet(StringComparer.Ordinal);
            var deferredCycles = await context.ScheduleEnrollmentDeferrals
                .AsNoTracking()
                .Where(deferral => deferral.AssetId == asset.Id)
                .Select(deferral => deferral.PmCycle)
                .ToListAsync(cancellationToken);
            var deferred = deferredCycles.ToHashSet(StringComparer.Ordinal);
            var created = 0;

            foreach (var identity in identities)
            {
                if (existing.Contains(identity.PmCycle) || deferred.Contains(identity.PmCycle))
                {
                    continue;
                }

                var lockReason = await GetBatchLockReasonAsync(context, identity, cancellationToken);
                if (lockReason is not null)
                {
                    context.ScheduleEnrollmentDeferrals.Add(CreateDeferral(asset, identity.PmCycle, now, lockReason));
                    continue;
                }

                context.PreventiveMaintenanceSchedules.Add(CreateSchedule(asset, identity.PmCycle, now));
                created++;
            }

            return created;
        }
        finally
        {
            foreach (var lease in leases.AsEnumerable().Reverse())
            {
                await lease.DisposeAsync();
            }
        }
    }

    public async Task<ScheduleGenerationResult> EnsureYearAsync(
        int year,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        if (year != institutionalNow.Year)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Schedule generation is limited to the current institutional calendar year.");
        }

        await using var readContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        var eligibleAssets = await readContext.Assets
            .AsNoTracking()
            .Where(asset => asset.Status == AssetStatusCatalog.Active
                && asset.Department != null
                && asset.Department.Trim() != string.Empty)
            .ToListAsync(cancellationToken);
        var eligible = eligibleAssets.Where(IsEligible)
            .Where(asset => PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt).Year <= year)
            .ToArray();

        var applicableCycles = eligible
            .SelectMany(asset =>
            {
                var created = PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt);
                var firstRegistrationMonth = created.Year == year ? created.Month : 1;
                return CpmpScheduleFrequency.GetMonths(asset.AssetCategory)
                    .Where(month => month >= firstRegistrationMonth)
                    .Select(month => (Asset: asset, Cycle: $"{year:D4}-{month:D2}", Month: month));
            })
            .ToArray();

        var coverageReviewItems = await FindCoverageReviewItemsAsync(
            readContext,
            eligible,
            year,
            institutionalNow.Month,
            cancellationToken);

        var work = applicableCycles
            .Where(item => effectiveDate is not null
                ? IsWithinApprovedCoverage(item.Cycle)
                : item.Month >= institutionalNow.Month)
            .Select(item => (item.Asset, item.Cycle))
            .GroupBy(item =>
            {
                ScheduleBatchIdentity.TryCreate(
                    item.Asset.Department, item.Asset.AssetCategory, item.Cycle, out var identity);
                return identity;
            })
            .OrderBy(group => group.Key.Department, StringComparer.Ordinal)
            .ThenBy(group => group.Key.AssetCategory, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PmCycle, StringComparer.Ordinal)
            .ToArray();

        var existingCount = 0;
        var createdCount = 0;
        var deferredCount = 0;
        foreach (var batch in work)
        {
            var result = await EnsureBatchAsync(batch.Key, batch.Select(item => item.Asset.Id).ToArray(), now, cancellationToken);
            existingCount += result.Existing;
            createdCount += result.Created;
            deferredCount += result.Deferred;
        }

        return new ScheduleGenerationResult(
            year,
            eligible.Length,
            existingCount,
            createdCount,
            deferredCount,
            coverageReviewItems.Length);
    }

    public async Task<(int Year, IReadOnlyList<ScheduleCoverageReviewItem> Items)> GetCoverageReviewAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var eligible = await context.Assets
            .AsNoTracking()
            .Where(asset => asset.Status == AssetStatusCatalog.Active
                && asset.Department != null
                && asset.Department.Trim() != string.Empty)
            .ToListAsync(cancellationToken);
        var eligibleAssets = eligible
            .Where(IsEligible)
            .Where(asset => PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt).Year <= institutionalNow.Year)
            .ToArray();
        var items = await FindCoverageReviewItemsAsync(
            context,
            eligibleAssets,
            institutionalNow.Year,
            institutionalNow.Month,
            cancellationToken);
        return (institutionalNow.Year, items);
    }

    private async Task<ScheduleCoverageReviewItem[]> FindCoverageReviewItemsAsync(
        ApplicationDbContext context,
        Asset[] eligible,
        int year,
        int currentMonth,
        CancellationToken cancellationToken)
    {
        if (effectiveDate is not null || currentMonth <= 1)
        {
            return [];
        }

        var uncoveredPastCycles = eligible
            .SelectMany(asset =>
            {
                var created = PreventiveMaintenanceCycle.ToInstitutionalTime(asset.CreatedAt);
                var firstRegistrationMonth = created.Year == year ? created.Month : 1;
                return CpmpScheduleFrequency.GetMonths(asset.AssetCategory)
                    .Where(month => month >= firstRegistrationMonth && month < currentMonth)
                    .Select(month => (Asset: asset, PmCycle: $"{year:D4}-{month:D2}"));
            })
            .ToArray();
        if (uncoveredPastCycles.Length == 0)
        {
            return [];
        }

        var pmCycles = uncoveredPastCycles
            .Select(item => item.PmCycle)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existingAndDeferredKeys = new HashSet<(Guid AssetId, string PmCycle)>();
        foreach (var assetIds in eligible.Select(asset => asset.Id).Chunk(PastCycleLookupBatchSize))
        {
            var existingKeys = await context.PreventiveMaintenanceSchedules
                .AsNoTracking()
                .Where(schedule => assetIds.Contains(schedule.AssetId)
                    && pmCycles.Contains(schedule.PmCycle))
                .Select(schedule => new { schedule.AssetId, schedule.PmCycle })
                .ToListAsync(cancellationToken);
            var deferredKeys = await context.ScheduleEnrollmentDeferrals
                .AsNoTracking()
                .Where(deferral => assetIds.Contains(deferral.AssetId)
                    && pmCycles.Contains(deferral.PmCycle))
                .Select(deferral => new { deferral.AssetId, deferral.PmCycle })
                .ToListAsync(cancellationToken);
            existingAndDeferredKeys.UnionWith(
                existingKeys.Select(item => (item.AssetId, item.PmCycle)));
            existingAndDeferredKeys.UnionWith(
                deferredKeys.Select(item => (item.AssetId, item.PmCycle)));
        }

        const string reason = "No schedule or enrollment deferral exists for this past PM cycle, and the approved coverage start date is not configured.";
        return uncoveredPastCycles
            .Where(item => !existingAndDeferredKeys.Contains((item.Asset.Id, item.PmCycle)))
            .Select(item => new ScheduleCoverageReviewItem(
                item.Asset.Id,
                item.Asset.AssetCode,
                item.Asset.Department!.Trim(),
                item.Asset.AssetCategory,
                item.PmCycle,
                reason))
            .OrderBy(item => item.Department, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.AssetCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PmCycle, StringComparer.Ordinal)
            .ThenBy(item => item.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsWithinApprovedCoverage(string pmCycle)
    {
        if (effectiveDate is null)
        {
            return true;
        }

        var deadline = PreventiveMaintenanceCycle.ToInstitutionalTime(
            PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle));
        return DateOnly.FromDateTime(deadline.DateTime) >= effectiveDate.Value;
    }

    private async Task<BatchGenerationResult> EnsureBatchAsync(
        ScheduleBatchIdentity identity,
        Guid[] candidateAssetIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxUniqueConflictRetries; attempt++)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var lease = await ScheduleBatchMutationLockLease.AcquireAsync(
                context, identity, cancellationToken);
            if (!lease.Acquired)
            {
                throw new ScheduleBatchMutationConflictException();
            }

            var assets = await context.Assets
                .Where(asset => candidateAssetIds.Contains(asset.Id)
                    && asset.Status == AssetStatusCatalog.Active
                    && asset.Department != null
                    && asset.Department.Trim().ToUpper() == identity.Department
                    && asset.AssetCategory.Trim().ToUpper() == identity.AssetCategory)
                .ToListAsync(cancellationToken);
            if (assets.Count == 0)
            {
                await lease.CommitAsync(cancellationToken);
                return default;
            }

            var existingAssetIds = await context.PreventiveMaintenanceSchedules
                .Where(schedule => schedule.PmCycle == identity.PmCycle
                    && candidateAssetIds.Contains(schedule.AssetId))
                .Select(schedule => schedule.AssetId)
                .ToListAsync(cancellationToken);
            var existing = existingAssetIds.ToHashSet();
            var deferredAssetIds = await context.ScheduleEnrollmentDeferrals
                .Where(deferral => deferral.PmCycle == identity.PmCycle
                    && candidateAssetIds.Contains(deferral.AssetId))
                .Select(deferral => deferral.AssetId)
                .ToListAsync(cancellationToken);
            var deferred = deferredAssetIds.ToHashSet();
            var missing = assets.Where(asset => !existing.Contains(asset.Id) && !deferred.Contains(asset.Id)).ToArray();
            var lockReason = missing.Length > 0
                ? await GetBatchLockReasonAsync(context, identity, cancellationToken)
                : null;
            var created = 0;
            var newDeferrals = 0;
            foreach (var asset in missing)
            {
                if (lockReason is not null)
                {
                    context.ScheduleEnrollmentDeferrals.Add(CreateDeferral(asset, identity.PmCycle, now, lockReason));
                    newDeferrals++;
                }
                else
                {
                    context.PreventiveMaintenanceSchedules.Add(CreateSchedule(asset, identity.PmCycle, now));
                    created++;
                }
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                await lease.CommitAsync(cancellationToken);
                return new BatchGenerationResult(existing.Count, created, deferred.Count + newDeferrals);
            }
            catch (DbUpdateException exception)
                when (DatabaseConstraintViolation.IsUniqueConstraint(exception, UniqueIndexName)
                    && attempt + 1 < MaxUniqueConflictRetries)
            {
                // Reload the authoritative schedule set under the same per-batch lock.
            }
            catch (DbUpdateException exception)
                when (DatabaseConstraintViolation.IsUniqueConstraint(exception)
                    && attempt + 1 < MaxUniqueConflictRetries)
            {
                // A concurrent registration inserted one of this batch's rows.
            }
        }

        throw new InvalidOperationException("Schedule generation could not settle after concurrent inserts.");
    }

    internal static async Task<string?> GetBatchLockReasonAsync(
        ApplicationDbContext context,
        ScheduleBatchIdentity identity,
        CancellationToken cancellationToken)
    {
        var schedules = await context.PreventiveMaintenanceSchedules
            .Include(schedule => schedule.Asset)
            .Where(schedule => schedule.PmCycle == identity.PmCycle
                && schedule.Asset != null
                && schedule.Asset.Department != null
                && schedule.Asset.Department.Trim().ToUpper() == identity.Department
                && schedule.Asset.AssetCategory.Trim().ToUpper() == identity.AssetCategory)
            .ToListAsync(cancellationToken);

        var formStatus = await context.PreventiveMaintenanceForms
            .Where(form =>
                (form.Status == PreventiveMaintenanceFormStatusCatalog.Submitted
                    || form.Status == PreventiveMaintenanceFormStatusCatalog.Acknowledged)
                && form.PmCycle == identity.PmCycle
                && form.Department != null
                && form.Department.Trim().ToUpper() == identity.Department
                && form.AssetCategory.Trim().ToUpper() == identity.AssetCategory)
            .Select(form => form.Status)
            .FirstOrDefaultAsync(cancellationToken);
        if (formStatus == PreventiveMaintenanceFormStatusCatalog.Acknowledged)
        {
            return ScheduleEnrollmentDeferralReason.FormAcknowledged;
        }

        if (formStatus == PreventiveMaintenanceFormStatusCatalog.Submitted)
        {
            return ScheduleEnrollmentDeferralReason.FormSubmitted;
        }

        if (schedules.Any(schedule => schedule.AssignedSupervisorUserId is not null
            || schedule.AssignedToUserId is not null
            || schedule.CompletedAt is not null
            || !string.Equals(schedule.Status, ScheduleStatusCatalog.Due, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(schedule.Status, ScheduleStatusCatalog.Overdue, StringComparison.OrdinalIgnoreCase)))
        {
            var assigned = schedules.Any(schedule => schedule.AssignedSupervisorUserId is not null
                || schedule.AssignedToUserId is not null);
            if (assigned)
            {
                return ScheduleEnrollmentDeferralReason.BatchAssigned;
            }

            if (schedules.Any(schedule => string.Equals(
                    schedule.Status, ScheduleStatusCatalog.Ongoing, StringComparison.OrdinalIgnoreCase)))
            {
                return ScheduleEnrollmentDeferralReason.WorkInProgress;
            }

            if (schedules.Any(schedule => string.Equals(
                    schedule.Status, ScheduleStatusCatalog.Cancelled, StringComparison.OrdinalIgnoreCase)))
            {
                return ScheduleEnrollmentDeferralReason.CycleCancelled;
            }

            if (schedules.Any(schedule => schedule.CompletedAt is not null
                || string.Equals(schedule.Status, ScheduleStatusCatalog.Completed, StringComparison.OrdinalIgnoreCase)))
            {
                return ScheduleEnrollmentDeferralReason.CycleCompleted;
            }
        }

        var scheduleIds = schedules.Select(schedule => schedule.Id).ToArray();
        var hasInspection = await context.InspectionRecords.AnyAsync(
            inspection => scheduleIds.Contains(inspection.ScheduleId),
            cancellationToken);
        if (hasInspection)
        {
            return ScheduleEnrollmentDeferralReason.InspectionStarted;
        }

        return null;
    }

    internal static ScheduleEnrollmentDeferral CreateDeferral(
        Asset asset,
        string deferredCycle,
        DateTimeOffset now,
        string reasonCode)
    {
        var months = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
        PreventiveMaintenanceCycle.TryParse(deferredCycle, out var year, out var month);
        var next = months.FirstOrDefault(candidate => candidate > month);
        var nextYear = year;
        if (next == 0)
        {
            next = months[0];
            nextYear++;
        }

        return new ScheduleEnrollmentDeferral
        {
            AssetId = asset.Id,
            PmCycle = deferredCycle,
            DepartmentAtDeferral = asset.Department!.Trim().ToUpperInvariant(),
            AssetCategoryAtDeferral = asset.AssetCategory,
            ReasonCode = reasonCode,
            DeferredAt = now,
            NextEligiblePmCycle = $"{nextYear:D4}-{next:D2}"
        };
    }

    internal static PreventiveMaintenanceSchedule CreateSchedule(
        Asset asset,
        string pmCycle,
        DateTimeOffset now)
    {
        PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
        var months = CpmpScheduleFrequency.GetMonths(asset.AssetCategory);
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(pmCycle);
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);
        var quarterly = months.Count == 4;
        return new PreventiveMaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            Asset = asset,
            ScheduleDate = deadline,
            PmCycle = pmCycle,
            PeriodType = quarterly ? SchedulePeriodTypeCatalog.Quarter : SchedulePeriodTypeCatalog.Semester,
            Quarter = quarterly ? $"Q{((month - 1) / 3) + 1}" : null,
            Year = year,
            Status = deadline < institutionalNow ? ScheduleStatusCatalog.Overdue : ScheduleStatusCatalog.Due,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static bool IsEligible(Asset asset)
    {
        return asset.Status == AssetStatusCatalog.Active
            && !string.IsNullOrWhiteSpace(asset.Department)
            && CpmpScheduleFrequency.GetMonths(asset.AssetCategory).Count > 0;
    }

    private readonly record struct BatchGenerationResult(int Existing, int Created, int Deferred);
}
