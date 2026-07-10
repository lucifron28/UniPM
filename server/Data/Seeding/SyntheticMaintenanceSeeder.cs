using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Models;

namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticMaintenanceSeeder(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    SyntheticMaintenanceDatasetLoader datasetLoader)
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8);

    public async Task<SyntheticMaintenanceSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var dataset = await datasetLoader.LoadAsync(cancellationToken);

        await using var context = await CreateReadyContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);

        await ValidateDatabaseConflictsAsync(context, dataset, cancellationToken);

        var existingAssets = await context.Assets
            .Where(asset => dataset.Assets.Select(seed => seed.Id).Contains(asset.Id))
            .ToDictionaryAsync(asset => asset.Id, cancellationToken);

        foreach (var seedAsset in dataset.Assets)
        {
            if (!existingAssets.TryGetValue(seedAsset.Id, out var asset))
            {
                asset = new Asset { Id = seedAsset.Id };
                context.Assets.Add(asset);
            }

            Apply(seedAsset, asset, dataset.CreatedAt);
        }

        await context.SaveChangesAsync(cancellationToken);

        var existingSchedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => dataset.Schedules.Select(seed => seed.Id).Contains(schedule.Id))
            .ToDictionaryAsync(schedule => schedule.Id, cancellationToken);

        foreach (var seedSchedule in dataset.Schedules)
        {
            if (!existingSchedules.TryGetValue(seedSchedule.Id, out var schedule))
            {
                schedule = new PreventiveMaintenanceSchedule { Id = seedSchedule.Id };
                context.PreventiveMaintenanceSchedules.Add(schedule);
            }

            Apply(seedSchedule, schedule, dataset.CreatedAt);
        }

        await context.SaveChangesAsync(cancellationToken);

        var existingInspections = await context.InspectionRecords
            .Where(inspection => dataset.Inspections.Select(seed => seed.Id).Contains(inspection.Id))
            .ToDictionaryAsync(inspection => inspection.Id, cancellationToken);

        foreach (var seedInspection in dataset.Inspections)
        {
            if (!existingInspections.TryGetValue(seedInspection.Id, out var inspection))
            {
                inspection = new InspectionRecord { Id = seedInspection.Id };
                context.InspectionRecords.Add(inspection);
            }

            Apply(seedInspection, inspection, dataset.CreatedAt);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new SyntheticMaintenanceSeedResult(
            dataset.Assets.Count,
            dataset.Schedules.Count,
            dataset.Inspections.Count);
    }

    public async Task<SyntheticMaintenanceResetResult> ResetAsync(CancellationToken cancellationToken = default)
    {
        var dataset = await datasetLoader.LoadAsync(cancellationToken);

        await using var context = await CreateReadyContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);

        var inspectionIds = dataset.Inspections.Select(inspection => inspection.Id).ToHashSet();
        var scheduleIds = dataset.Schedules.Select(schedule => schedule.Id).ToHashSet();
        var assetIds = dataset.Assets.Select(asset => asset.Id).ToHashSet();

        var inspections = await context.InspectionRecords
            .Where(inspection => inspectionIds.Contains(inspection.Id))
            .ToListAsync(cancellationToken);
        context.InspectionRecords.RemoveRange(inspections);
        await context.SaveChangesAsync(cancellationToken);

        var schedules = await context.PreventiveMaintenanceSchedules
            .Where(schedule => scheduleIds.Contains(schedule.Id))
            .ToListAsync(cancellationToken);
        context.PreventiveMaintenanceSchedules.RemoveRange(schedules);
        await context.SaveChangesAsync(cancellationToken);

        var assets = await context.Assets
            .Where(asset => assetIds.Contains(asset.Id))
            .ToListAsync(cancellationToken);
        context.Assets.RemoveRange(assets);
        await context.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new SyntheticMaintenanceResetResult(assets.Count, schedules.Count, inspections.Count);
    }

    private async Task<ApplicationDbContext> CreateReadyContextAsync(CancellationToken cancellationToken)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The database is not reachable for synthetic seeding.");
            }

            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync(cancellationToken);
            }

            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private static async Task ValidateDatabaseConflictsAsync(
        ApplicationDbContext context,
        SyntheticMaintenanceDataset dataset,
        CancellationToken cancellationToken)
    {
        var assetCodes = dataset.Assets.Select(asset => asset.AssetCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var qrCodeValues = dataset.Assets.Select(asset => asset.QrCodeValue).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fixtureAssetIds = dataset.Assets.Select(asset => asset.Id).ToHashSet();

        var conflictingAssets = await context.Assets
            .Where(asset => assetCodes.Contains(asset.AssetCode) || (asset.QrCodeValue != null && qrCodeValues.Contains(asset.QrCodeValue)))
            .ToListAsync(cancellationToken);

        if (conflictingAssets.Any(asset => !fixtureAssetIds.Contains(asset.Id)))
        {
            throw new SyntheticMaintenanceFixtureException(
                "A fixture asset code or QR value conflicts with an unrelated database asset.");
        }
    }

    private static void Apply(SyntheticAsset source, Asset target, DateTimeOffset timestamp)
    {
        target.AssetCode = source.AssetCode;
        target.AssetCategory = source.AssetCategory;
        target.Building = source.Building;
        target.Department = source.Department;
        target.Location = source.Location;
        target.QrCodeValue = source.QrCodeValue;
        target.Status = source.Status;
        target.CreatedAt = timestamp;
        target.UpdatedAt = timestamp;
    }

    private static void Apply(
        SyntheticSchedule source,
        PreventiveMaintenanceSchedule target,
        DateTimeOffset timestamp)
    {
        target.AssetId = source.AssetId;
        target.ScheduleDate = AtManilaMidnight(source.ScheduleDate);
        target.PeriodType = source.PeriodType;
        target.Quarter = source.Quarter;
        target.Semester = source.Semester;
        target.Year = source.Year;
        target.AcademicYear = source.AcademicYear;
        target.Status = source.Status;
        target.AssignedToUserId = source.AssignedToUserId;
        target.CompletedAt = source.CompletedAt;
        target.CreatedAt = timestamp;
        target.UpdatedAt = timestamp;
    }

    private static void Apply(
        SyntheticInspection source,
        InspectionRecord target,
        DateTimeOffset timestamp)
    {
        target.ScheduleId = source.ScheduleId;
        target.AssetId = source.AssetId;
        target.InspectorUserId = source.InspectorUserId;
        target.DateInspected = source.DateInspected;
        target.IsOperational = source.IsOperational;
        target.Remarks = source.Remarks;
        target.ActionsRecommendations = source.ActionsRecommendations;
        target.CreatedAt = timestamp;
        target.UpdatedAt = timestamp;
    }

    private static DateTimeOffset AtManilaMidnight(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), ManilaOffset);
    }
}

public sealed record SyntheticMaintenanceSeedResult(int Assets, int Schedules, int Inspections);

public sealed record SyntheticMaintenanceResetResult(int AssetsRemoved, int SchedulesRemoved, int InspectionsRemoved);
