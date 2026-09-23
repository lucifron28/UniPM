using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Reports;

internal sealed class PmPeriodDashboardService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
{
    internal async Task<IReadOnlyList<PmPeriodDashboardCycleGroupResponse>>
        GetAvailableCyclesAsync(
            string? assetCategory,
            int? year,
            CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var schedules = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Include(schedule => schedule.Asset)
            .Where(schedule => schedule.Status != ScheduleStatusCatalog.Cancelled)
            .ToListAsync(cancellationToken);

        var cycles = schedules
            .Select(ToCycleSnapshot)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .Where(snapshot => assetCategory is null
                || string.Equals(snapshot.AssetCategory, assetCategory, StringComparison.Ordinal))
            .Where(snapshot => year is null || snapshot.Year == year.Value)
            .GroupBy(snapshot => new { snapshot.AssetCategory, snapshot.Year })
            .OrderBy(group => group.Key.AssetCategory, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Year)
            .Select(group => new PmPeriodDashboardCycleGroupResponse(
                group.Key.AssetCategory,
                group.Key.Year,
                group
                    .GroupBy(snapshot => snapshot.PmCycle, StringComparer.Ordinal)
                    .OrderBy(cycle => cycle.Key, StringComparer.Ordinal)
                    .Select(cycle => new PmPeriodDashboardCycleOptionResponse(
                        cycle.Key,
                        cycle.Count()))
                    .ToArray()))
            .ToArray();

        return cycles;
    }

    internal async Task<PmPeriodDashboardResponse> GetPeriodAsync(
        PmPeriodDashboardQuery query,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var schedules = await context.PreventiveMaintenanceSchedules
            .AsNoTracking()
            .Include(schedule => schedule.Asset)
            .Where(schedule => schedule.Status != ScheduleStatusCatalog.Cancelled)
            .ToListAsync(cancellationToken);

        var candidateSchedules = schedules
            .Select(ToCycleSnapshot)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .Where(snapshot => string.Equals(snapshot.PmCycle, query.PmCycle, StringComparison.Ordinal)
                && string.Equals(snapshot.AssetCategory, query.AssetCategory, StringComparison.Ordinal))
            .ToArray();

        var department = NormalizeDepartment(query.Department);
        var authoritativeSchedules = candidateSchedules
            .Where(snapshot => department is null
                || string.Equals(snapshot.Department, department, StringComparison.Ordinal))
            .ToArray();

        var scheduleIds = candidateSchedules
            .Select(snapshot => snapshot.ScheduleId)
            .ToArray();
        IReadOnlyList<InspectionSnapshot> inspections = scheduleIds.Length == 0
            ? []
            : await context.InspectionRecords
                .AsNoTracking()
                .Where(inspection => scheduleIds.Contains(inspection.ScheduleId))
                .Select(inspection => new InspectionSnapshot(
                    inspection.Id,
                    inspection.ScheduleId,
                    inspection.PreventiveMaintenanceFormId,
                    inspection.CompletedAt,
                    inspection.IsOperational,
                    inspection.Remarks,
                    inspection.ActionsRecommendations))
                .ToListAsync(cancellationToken);

        var forms = await context.PreventiveMaintenanceForms
            .AsNoTracking()
            .Include(form => form.Acknowledgement)
            .Where(form => form.AssetCategory == query.AssetCategory
                && form.PmCycle == query.PmCycle)
            .ToListAsync(cancellationToken);

        var inspectionBySchedule = inspections
            .GroupBy(inspection => inspection.ScheduleId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(inspection => inspection.CompletedAt)
                    .ThenBy(inspection => inspection.InspectionId)
                    .First());
        var formById = forms.ToDictionary(form => form.Id);
        var now = timeProvider.GetUtcNow();
        var deadline = PreventiveMaintenanceCycle.DeadlineForCycle(query.PmCycle);
        var periodState = ResolvePeriodState(query.PmCycle, deadline, now);
        var authoritativeRows = authoritativeSchedules
            .Select(snapshot => ToDashboardRow(
                snapshot,
                inspectionBySchedule.GetValueOrDefault(snapshot.ScheduleId),
                formById,
                forms,
                periodState,
                deadline))
            .ToArray();

        var displayRows = authoritativeRows
            .Where(row => MatchesDisplayFilters(row, query))
            .ToArray();

        var scheduled = authoritativeRows.Length;
        var inspected = authoritativeRows.Count(row => row.IsInspected);
        var completedOnTime = authoritativeRows.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.OnTime);
        var completedLate = authoritativeRows.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.Late);
        var notCompleted = authoritativeRows.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.NotCompleted);
        var remaining = authoritativeRows.Count(row => row.Timeliness is
            PmPeriodDashboardFilterCatalog.Scheduled or
            PmPeriodDashboardFilterCatalog.Pending);
        var operational = authoritativeRows.Count(row => row.Condition == PmPeriodDashboardFilterCatalog.Operational);
        var nonOperational = authoritativeRows.Count(row => row.Condition == PmPeriodDashboardFilterCatalog.NonOperational);
        var complianceMeasurable = periodState == PmPeriodDashboardPeriodStateCatalog.Closed;
        decimal? onTimeCompliancePercent = complianceMeasurable && scheduled > 0
            ? ToPercent(completedOnTime, scheduled)
            : null;

        var batches = authoritativeRows
            .GroupBy(row => new BatchKey(
                NormalizeDepartment(row.Department),
                row.AssetCategory,
                row.PmCycle))
            .OrderBy(group => group.Key.Department, StringComparer.Ordinal)
            .ThenBy(group => group.Key.AssetCategory, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PmCycle, StringComparer.Ordinal)
            .Select(group => ToBatchResponse(group, forms, complianceMeasurable))
            .ToArray();

        var normalizedDepartment = department;

        return new PmPeriodDashboardResponse(
            query.PmCycle,
            query.AssetCategory,
            normalizedDepartment,
            deadline,
            periodState,
            complianceMeasurable,
            inspected > 0,
            scheduled,
            inspected,
            completedOnTime,
            completedLate,
            notCompleted,
            remaining,
            operational,
            nonOperational,
            onTimeCompliancePercent,
            scheduled == 0 ? 0 : ToPercent(inspected, scheduled),
            batches,
            displayRows.Select(row => row.Response).ToArray());
    }

    private static CycleSnapshot? ToCycleSnapshot(PreventiveMaintenanceSchedule schedule)
    {
        if (schedule.Asset is null)
        {
            return null;
        }

        var pmCycle = PreventiveMaintenanceCycle.ForSchedule(schedule);
        var department = NormalizeDepartment(schedule.Asset.Department);
        if (department is null
            || !PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out _)
            || !AssetCategoryCatalog.TryNormalize(schedule.Asset.AssetCategory, out var assetCategory))
        {
            return null;
        }

        return new CycleSnapshot(
            schedule.Id,
            schedule.AssetId,
            assetCategory,
            department,
            pmCycle,
            year,
            schedule.ScheduleDate,
            schedule.Status,
            schedule.Asset.AssetCode,
            schedule.Asset.Building,
            schedule.Asset.Location);
    }

    private static DashboardRow ToDashboardRow(
        CycleSnapshot snapshot,
        InspectionSnapshot? inspection,
        IReadOnlyDictionary<Guid, PreventiveMaintenanceForm> formById,
        IReadOnlyList<PreventiveMaintenanceForm> forms,
        string periodState,
        DateTimeOffset deadline)
    {
        var isInspected = inspection?.CompletedAt is not null;
        var timeliness = !isInspected
            ? periodState switch
            {
                PmPeriodDashboardPeriodStateCatalog.Future => PmPeriodDashboardFilterCatalog.Scheduled,
                PmPeriodDashboardPeriodStateCatalog.Active => PmPeriodDashboardFilterCatalog.Pending,
                _ => PmPeriodDashboardFilterCatalog.NotCompleted
            }
            : inspection!.CompletedAt <= deadline
                ? PmPeriodDashboardFilterCatalog.OnTime
                : PmPeriodDashboardFilterCatalog.Late;
        var condition = !isInspected
            ? PmPeriodDashboardFilterCatalog.NotInspected
            : inspection!.IsOperational
                ? PmPeriodDashboardFilterCatalog.Operational
                : PmPeriodDashboardFilterCatalog.NonOperational;

        var executionStatus = isInspected
            ? "Completed"
            : timeliness switch
            {
                PmPeriodDashboardFilterCatalog.Scheduled => PmPeriodDashboardFilterCatalog.Scheduled,
                PmPeriodDashboardFilterCatalog.Pending => PmPeriodDashboardFilterCatalog.Pending,
                _ => PmPeriodDashboardFilterCatalog.NotCompleted
            };

        var form = ResolveForm(snapshot, inspection, formById, forms);
        var response = new PmPeriodDashboardAssetRowResponse(
            snapshot.ScheduleId,
            snapshot.AssetId,
            inspection?.InspectionId,
            snapshot.AssetCode,
            snapshot.AssetCategory,
            snapshot.Building,
            snapshot.Location,
            snapshot.Department,
            snapshot.PmCycle,
            snapshot.ScheduleDate,
            deadline,
            snapshot.ScheduleStatus,
            executionStatus,
            isInspected,
            inspection?.CompletedAt,
            timeliness,
            condition,
            inspection?.Remarks,
            inspection?.ActionsRecommendations,
            form?.Id,
            form?.Status,
            form?.Acknowledgement is not null,
            form?.Acknowledgement?.AcknowledgedAt);

        return new DashboardRow(
            snapshot.Department,
            snapshot.AssetCategory,
            snapshot.PmCycle,
            response,
            form,
            isInspected,
            timeliness,
            condition,
            snapshot,
            inspection);
    }

    private static bool MatchesDisplayFilters(
        DashboardRow row,
        PmPeriodDashboardQuery query)
    {
        return (query.Condition is null || query.Condition == row.Condition)
            && (query.Timeliness is null || query.Timeliness == row.Timeliness)
            && MatchesSearch(query.Search, row.Snapshot, row.Inspection);
    }

    private static PreventiveMaintenanceForm? ResolveForm(
        CycleSnapshot snapshot,
        InspectionSnapshot? inspection,
        IReadOnlyDictionary<Guid, PreventiveMaintenanceForm> formById,
        IReadOnlyList<PreventiveMaintenanceForm> forms)
    {
        if (inspection?.FormId is { } formId
            && formById.TryGetValue(formId, out var linkedForm)
            && IsFormCompatible(linkedForm, snapshot))
        {
            return linkedForm;
        }

        return forms
            .Where(form => IsFormCompatible(form, snapshot))
            .OrderByDescending(form => string.Equals(form.PmCycle, snapshot.PmCycle, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(form => form.CreatedAt)
            .ThenBy(form => form.Id)
            .FirstOrDefault();
    }

    private static bool IsFormCompatible(
        PreventiveMaintenanceForm form,
        CycleSnapshot snapshot)
    {
        return string.Equals(
                NormalizeDepartment(form.Department),
                snapshot.Department,
                StringComparison.Ordinal)
            && string.Equals(form.AssetCategory, snapshot.AssetCategory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(form.PmCycle, snapshot.PmCycle, StringComparison.Ordinal);
    }

    private static PmPeriodDashboardBatchResponse ToBatchResponse(
        IGrouping<BatchKey, DashboardRow> group,
        IReadOnlyList<PreventiveMaintenanceForm> forms,
        bool complianceMeasurable)
    {
        var form = group
            .Select(row => row.Form)
            .FirstOrDefault(candidate => candidate is not null)
            ?? forms
                .Where(candidate => string.Equals(
                    NormalizeDepartment(candidate.Department),
                    group.Key.Department,
                    StringComparison.Ordinal)
                    && string.Equals(candidate.AssetCategory, group.Key.AssetCategory, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.PmCycle, group.Key.PmCycle, StringComparison.Ordinal))
                .OrderByDescending(candidate => string.Equals(candidate.PmCycle, group.Key.PmCycle, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();

        var scheduled = group.Count();
        var completedOnTime = group.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.OnTime);
        var fieldWorkCompletedAt = form is null
            ? null
            : group
                .Where(row => row.Inspection?.FormId == form.Id)
                .Select(row => row.Inspection?.CompletedAt)
                .Max();
        decimal? onTimeCompliancePercent = complianceMeasurable && scheduled > 0
            ? ToPercent(completedOnTime, scheduled)
            : null;

        return new PmPeriodDashboardBatchResponse(
            group.Key.Department,
            group.Key.AssetCategory,
            group.Key.PmCycle,
            scheduled,
            group.Count(row => row.IsInspected),
            completedOnTime,
            onTimeCompliancePercent,
            group.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.Late),
            group.Count(row => row.Timeliness == PmPeriodDashboardFilterCatalog.NotCompleted),
            group.Count(row => row.Timeliness is
                PmPeriodDashboardFilterCatalog.Scheduled or
                PmPeriodDashboardFilterCatalog.Pending),
            form?.Id,
            form?.Status,
            form?.FileNumber,
            fieldWorkCompletedAt,
            form?.SubmittedAt,
            form?.Acknowledgement is not null,
            form?.Acknowledgement?.AcknowledgedAt);
    }

    private static bool MatchesSearch(
        string? search,
        CycleSnapshot snapshot,
        InspectionSnapshot? inspection)
    {
        if (search is null)
        {
            return true;
        }

        return Contains(snapshot.AssetCode, search)
            || Contains(snapshot.AssetCategory, search)
            || Contains(snapshot.Department, search)
            || Contains(snapshot.Building, search)
            || Contains(snapshot.Location, search)
            || Contains(snapshot.PmCycle, search)
            || (inspection is not null && Contains(inspection.InspectionId.ToString(), search));
    }

    private static bool Contains(string? value, string search)
    {
        return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolvePeriodState(
        string pmCycle,
        DateTimeOffset deadline,
        DateTimeOffset now)
    {
        PreventiveMaintenanceCycle.TryParse(pmCycle, out var year, out var month);
        var cycleStart = new DateTimeOffset(
            year,
            month,
            1,
            0,
            0,
            0,
            PreventiveMaintenanceCycle.InstitutionalOffset);
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(now);

        if (institutionalNow < cycleStart)
        {
            return PmPeriodDashboardPeriodStateCatalog.Future;
        }

        return institutionalNow > deadline
            ? PmPeriodDashboardPeriodStateCatalog.Closed
            : PmPeriodDashboardPeriodStateCatalog.Active;
    }

    private static decimal ToPercent(int numerator, int denominator)
    {
        return Math.Round((decimal)numerator * 100 / denominator, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeDepartment(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private sealed record CycleSnapshot(
        Guid ScheduleId,
        Guid AssetId,
        string AssetCategory,
        string? Department,
        string PmCycle,
        int Year,
        DateTimeOffset ScheduleDate,
        string ScheduleStatus,
        string AssetCode,
        string? Building,
        string? Location);

    private sealed record InspectionSnapshot(
        Guid InspectionId,
        Guid ScheduleId,
        Guid? FormId,
        DateTimeOffset? CompletedAt,
        bool IsOperational,
        string? Remarks,
        string? ActionsRecommendations);

    private sealed record DashboardRow(
        string? Department,
        string AssetCategory,
        string PmCycle,
        PmPeriodDashboardAssetRowResponse Response,
        PreventiveMaintenanceForm? Form,
        bool IsInspected,
        string Timeliness,
        string Condition,
        CycleSnapshot Snapshot,
        InspectionSnapshot? Inspection);

    private readonly record struct BatchKey(
        string? Department,
        string AssetCategory,
        string PmCycle);
}
