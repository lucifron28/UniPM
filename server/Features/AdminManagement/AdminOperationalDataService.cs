using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Data.Seeding;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Inspections;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.AdminManagement;

internal sealed class AdminOperationalDataService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    IInspectionPhotoEvidenceStorage photoStorage,
    TimeProvider timeProvider,
    ILogger<AdminOperationalDataService> logger)
{
    private static readonly SemaphoreSlim DemoDataMutationGate = new(1, 1);

    public async Task<AdminOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new AdminOverviewResponse(
            await context.Users.CountAsync(user => user.IsActive, cancellationToken),
            await context.Users.CountAsync(user => !user.IsActive, cancellationToken),
            await context.Assets.CountAsync(cancellationToken),
            await context.PreventiveMaintenanceSchedules.CountAsync(cancellationToken),
            await context.InspectionRecords.CountAsync(cancellationToken),
            await context.PreventiveMaintenanceForms.CountAsync(
                form => form.Status == PreventiveMaintenanceFormStatusCatalog.Draft, cancellationToken),
            await context.PreventiveMaintenanceForms.CountAsync(
                form => form.Status == PreventiveMaintenanceFormStatusCatalog.Submitted, cancellationToken),
            await context.PreventiveMaintenanceForms.CountAsync(
                form => form.Status == PreventiveMaintenanceFormStatusCatalog.Acknowledged, cancellationToken),
            await context.PreventiveMaintenanceAcknowledgements.CountAsync(cancellationToken));
    }

    public async Task<AdminAssetPageResponse> ListAssetsAsync(
        string? search,
        string? category,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Assets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(asset => asset.AssetCode.Contains(value)
                || (asset.Building != null && asset.Building.Contains(value))
                || (asset.Department != null && asset.Department.Contains(value))
                || (asset.Location != null && asset.Location.Contains(value)));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(asset => asset.AssetCategory == category.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(asset => asset.Status == status.Trim());

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(asset => asset.AssetCode)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(asset => new AdminAssetRow(
                asset.Id, asset.AssetCode, asset.AssetCategory, asset.Building,
                asset.Department, asset.Location, asset.QrCodeValue, asset.Status,
                asset.CreatedAt, asset.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new AdminAssetPageResponse(total, page, pageSize, items);
    }

    public async Task<AdminSchedulePageResponse> ListSchedulesAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<PreventiveMaintenanceSchedule> query = context.PreventiveMaintenanceSchedules
            .AsNoTracking().Include(schedule => schedule.Asset);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(schedule => (schedule.Asset != null
                    && (schedule.Asset.AssetCode.Contains(value)
                        || (schedule.Asset.Department != null && schedule.Asset.Department.Contains(value))))
                || schedule.PmCycle.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(schedule => schedule.Status == status.Trim());

        var total = await query.CountAsync(cancellationToken);
        var schedules = await query.OrderByDescending(schedule => schedule.ScheduleDate)
            .ThenBy(schedule => schedule.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var userIds = schedules.SelectMany(schedule => new[]
                { schedule.AssignedToUserId, schedule.AssignedSupervisorUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var users = await context.Users.AsNoTracking().Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return new AdminSchedulePageResponse(total, page, pageSize, schedules.Select(schedule =>
            new AdminScheduleRow(
                schedule.Id, schedule.AssetId, schedule.Asset?.AssetCode ?? "",
                schedule.Asset?.AssetCategory ?? "", schedule.Asset?.Department,
                schedule.ScheduleDate, schedule.PmCycle, schedule.PeriodType,
                schedule.Status, schedule.Quarter, schedule.Semester, schedule.Year,
                schedule.AcademicYear, schedule.AssignedToUserId,
                schedule.AssignedToUserId is Guid inspectorId && users.TryGetValue(inspectorId, out var inspectorName)
                    ? inspectorName : null,
                schedule.AssignedSupervisorUserId,
                schedule.AssignedSupervisorUserId is Guid supervisorId && users.TryGetValue(supervisorId, out var supervisorName)
                    ? supervisorName : null,
                schedule.CompletedAt)).ToArray());
    }

    public async Task<AdminInspectionPageResponse> ListInspectionsAsync(
        string? search,
        string? formStatus,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<InspectionRecord> query = context.InspectionRecords.AsNoTracking()
            .Include(inspection => inspection.Asset)
            .Include(inspection => inspection.Schedule)
            .Include(inspection => inspection.PreventiveMaintenanceForm);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(inspection => inspection.Asset != null
                && (inspection.Asset.AssetCode.Contains(value)
                    || (inspection.Asset.Department != null && inspection.Asset.Department.Contains(value)))
                || inspection.Remarks != null && inspection.Remarks.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(formStatus))
            query = query.Where(inspection => inspection.PreventiveMaintenanceForm != null
                && inspection.PreventiveMaintenanceForm.Status == formStatus.Trim());

        var total = await query.CountAsync(cancellationToken);
        var inspections = await query.OrderByDescending(inspection => inspection.DateInspected)
            .ThenBy(inspection => inspection.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var inspectorIds = inspections.Select(inspection => inspection.InspectorUserId).Distinct().ToArray();
        var users = await context.Users.AsNoTracking().Where(user => inspectorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);
        return new AdminInspectionPageResponse(total, page, pageSize, inspections.Select(inspection =>
            new AdminInspectionRow(
                inspection.Id, inspection.ScheduleId, inspection.AssetId,
                inspection.Asset?.AssetCode ?? "", inspection.DateInspected,
                inspection.InspectorUserId,
                users.GetValueOrDefault(inspection.InspectorUserId),
                inspection.IsOperational, inspection.PreventiveMaintenanceFormId,
                inspection.PreventiveMaintenanceForm?.Status,
                inspection.PreventiveMaintenanceForm?.FileNumber)).ToArray());
    }

    public async Task<AdminFormPageResponse> ListFormsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<PreventiveMaintenanceForm> query = context.PreventiveMaintenanceForms
            .AsNoTracking().Include(form => form.Inspections);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(form => (form.FileNumber != null && form.FileNumber.Contains(value))
                || form.AssetCategory.Contains(value)
                || (form.Building != null && form.Building.Contains(value))
                || (form.Department != null && form.Department.Contains(value))
                || form.Inspections.Any(inspection => inspection.Asset != null
                    && inspection.Asset.AssetCode.Contains(value)));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(form => form.Status == status.Trim());

        var total = await query.CountAsync(cancellationToken);
        var forms = await query.OrderByDescending(form => form.UpdatedAt)
            .ThenBy(form => form.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var formIds = forms.Select(form => form.Id).ToArray();
        var acknowledgedIds = await context.PreventiveMaintenanceAcknowledgements.AsNoTracking()
            .Where(acknowledgement => formIds.Contains(acknowledgement.FormId))
            .Select(acknowledgement => acknowledgement.FormId).ToHashSetAsync(cancellationToken);
        return new AdminFormPageResponse(total, page, pageSize, forms.Select(form =>
            new AdminFormRow(form.Id, form.FileNumber, form.AssetCategory, form.Building,
                form.Department, form.PmCycle, form.Status, form.Inspections.Count,
                acknowledgedIds.Contains(form.Id), form.UpdatedAt)).ToArray());
    }

    public Task<AdminDeleteResult> DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        DeleteAsync(assetIds: [assetId], deleteAssets: true, cancellationToken: cancellationToken);

    public Task<AdminDeleteResult> DeleteAssetsAsync(
        IReadOnlyCollection<Guid> assetIds,
        bool allAssets,
        CancellationToken cancellationToken) =>
        DeleteAsync(assetIds: assetIds, deleteAssets: true, allAssets: allAssets, cancellationToken: cancellationToken);

    public Task<AdminDeleteResult> DeleteSchedulesAsync(
        IReadOnlyCollection<Guid> scheduleIds,
        CancellationToken cancellationToken) =>
        DeleteAsync(scheduleIds: scheduleIds, cancellationToken: cancellationToken);

    public Task<AdminDeleteResult> DeleteInspectionsAsync(
        IReadOnlyCollection<Guid> inspectionIds,
        CancellationToken cancellationToken) =>
        DeleteAsync(inspectionIds: inspectionIds, cancellationToken: cancellationToken);

    public Task<AdminDeleteResult> DeleteFormAsync(Guid formId, CancellationToken cancellationToken) =>
        DeleteAsync(formIds: [formId], cancellationToken: cancellationToken);

    public async Task<AdminDeleteResult> ClearMaintenanceAsync(CancellationToken cancellationToken)
    {
        await DemoDataMutationGate.WaitAsync(cancellationToken);
        try
        {
            return await DeleteAsync(allMaintenance: true, cancellationToken: cancellationToken);
        }
        finally
        {
            DemoDataMutationGate.Release();
        }
    }

    public async Task<AdminDeleteResult> ClearAssetsAndMaintenanceAsync(CancellationToken cancellationToken)
    {
        await DemoDataMutationGate.WaitAsync(cancellationToken);
        try
        {
            return await DeleteAsync(allMaintenance: true, deleteAssets: true, allAssets: true,
                cancellationToken: cancellationToken);
        }
        finally
        {
            DemoDataMutationGate.Release();
        }
    }

    public Task<AdminValidationDatasetResponse> LoadValidationDatasetAsync(CancellationToken cancellationToken) =>
        SeedValidationDatasetAsync(resetExistingData: false, cancellationToken);

    public Task<AdminValidationDatasetResponse> ResetAndSeedValidationDatasetAsync(
        CancellationToken cancellationToken) =>
        SeedValidationDatasetAsync(resetExistingData: true, cancellationToken);

    private async Task<AdminValidationDatasetResponse> SeedValidationDatasetAsync(
        bool resetExistingData,
        CancellationToken cancellationToken)
    {
        await DemoDataMutationGate.WaitAsync(cancellationToken);
        try
        {
            Guid inspectorUserId = Guid.Empty;
            Guid supervisorUserId = Guid.Empty;
            Guid gsdUserId = Guid.Empty;
            GsdValidationDatasetContents? dataset = null;

            var deleted = await DeleteAsync(
                deleteAssets: resetExistingData,
                allAssets: resetExistingData,
                allMaintenance: resetExistingData,
                beforeDelete: async (context, token) =>
                {
                    if (!resetExistingData && await HasOperationalDataAsync(context, token))
                        throw new AdminDemoDataConflictException(
                            "Clear all assets and maintenance data before loading the validation dataset.");

                    var roleUsers = await (from assignment in context.UserRoles.AsNoTracking()
                                           join role in context.Roles.AsNoTracking()
                                               on assignment.RoleId equals role.Id
                                           join user in context.Users.AsNoTracking()
                                               on assignment.UserId equals user.Id
                                           where user.IsActive && role.Name != null
                                               && (role.Name == AuthRoleCatalog.Gsd
                                                   || role.Name == AuthRoleCatalog.Inspector
                                                   || role.Name == AuthRoleCatalog.Supervisor)
                                           select new { Role = role.Name, user.Id, user.DisplayName })
                        .ToListAsync(token);

                    var activeHeads = roleUsers.Where(user => user.Role == AuthRoleCatalog.Gsd)
                        .Select(user => user.Id).Distinct().ToArray();
                    if (activeHeads.Length != 1)
                        throw new AdminDemoDataConflictException(
                            "Set exactly one active GSD head before loading the validation dataset.");
                    gsdUserId = activeHeads[0];

                    var inspector = roleUsers.Where(user => user.Role == AuthRoleCatalog.Inspector)
                        .OrderBy(user => user.DisplayName, StringComparer.Ordinal)
                        .ThenBy(user => user.Id).FirstOrDefault();
                    if (inspector is null)
                        throw new AdminDemoDataConflictException(
                            "Create or reactivate an Inspector account before loading the validation dataset.");
                    inspectorUserId = inspector.Id;

                    var supervisor = roleUsers.Where(user => user.Role == AuthRoleCatalog.Supervisor)
                        .OrderBy(user => user.DisplayName, StringComparer.Ordinal)
                        .ThenBy(user => user.Id).FirstOrDefault();
                    if (supervisor is null)
                        throw new AdminDemoDataConflictException(
                            "Create or reactivate a Supervisor account before loading the validation dataset.");
                    supervisorUserId = supervisor.Id;
                },
                afterDelete: async (context, token) =>
                {
                    context.ChangeTracker.Clear();
                    dataset = GsdValidationDatasetBuilder.Build(
                        inspectorUserId, supervisorUserId, gsdUserId);
                    context.Assets.AddRange(dataset.Assets);
                    context.PreventiveMaintenanceSchedules.AddRange(dataset.Schedules);
                    context.PreventiveMaintenanceForms.AddRange(dataset.Forms);
                    context.InspectionRecords.AddRange(dataset.Inspections);
                    context.PreventiveMaintenanceAcknowledgements.AddRange(dataset.Acknowledgements);
                    context.InspectionWmsReferrals.AddRange(dataset.WmsReferrals);
                    context.InspectionWmsReferralAudits.AddRange(dataset.WmsReferralAudits);
                    await context.SaveChangesAsync(token);
                },
                cancellationToken: cancellationToken);

            if (dataset is null)
                throw new InvalidOperationException("The validation dataset was not created.");
            return new AdminValidationDatasetResponse(
                deleted, dataset.Assets.Count, dataset.Schedules.Count, dataset.Inspections.Count,
                dataset.Forms.Count, dataset.Acknowledgements.Count,
                dataset.WmsReferrals.Count, dataset.WmsReferralAudits.Count);
        }
        finally
        {
            DemoDataMutationGate.Release();
        }
    }

    private static async Task<bool> HasOperationalDataAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.Assets.AnyAsync(cancellationToken)
        || await context.PreventiveMaintenanceSchedules.AnyAsync(cancellationToken)
        || await context.InspectionRecords.AnyAsync(cancellationToken)
        || await context.PreventiveMaintenanceForms.AnyAsync(cancellationToken)
        || await context.PreventiveMaintenanceAcknowledgements.AnyAsync(cancellationToken)
        || await context.InspectionPhotoEvidence.AnyAsync(cancellationToken)
        || await context.InspectionWmsReferrals.AnyAsync(cancellationToken)
        || await context.InspectionWmsReferralAudits.AnyAsync(cancellationToken)
        || await context.InspectionLocationAttempts.AnyAsync(cancellationToken)
        || await context.ScheduleAssignmentReassignmentAudits.AnyAsync(cancellationToken)
        || await context.ScheduleEnrollmentDeferrals.AnyAsync(cancellationToken);

    public async Task<AdminDeleteResult> DeleteAcknowledgementAsync(Guid formId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var form = await context.PreventiveMaintenanceForms
            .Include(candidate => candidate.Acknowledgement)
            .SingleOrDefaultAsync(candidate => candidate.Id == formId, cancellationToken);
        if (form is null) return new AdminDeleteResult();
        if (form.Acknowledgement is null) return new AdminDeleteResult();
        context.PreventiveMaintenanceAcknowledgements.Remove(form.Acknowledgement);
        form.Status = PreventiveMaintenanceFormStatusCatalog.Submitted;
        form.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return new AdminDeleteResult(AcknowledgementsRemoved: 1);
    }

    public async Task<bool> ReturnFormToDraftAsync(Guid formId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var form = await context.PreventiveMaintenanceForms
            .SingleOrDefaultAsync(candidate => candidate.Id == formId, cancellationToken);
        if (form is null || form.Status != PreventiveMaintenanceFormStatusCatalog.Submitted) return false;
        form.Status = PreventiveMaintenanceFormStatusCatalog.Draft;
        form.SubmittedAt = null;
        form.SubmittedByUserId = null;
        form.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AdminDeleteResult> DeleteAsync(
        IReadOnlyCollection<Guid>? assetIds = null,
        IReadOnlyCollection<Guid>? scheduleIds = null,
        IReadOnlyCollection<Guid>? inspectionIds = null,
        IReadOnlyCollection<Guid>? formIds = null,
        bool deleteAssets = false,
        bool allAssets = false,
        bool allMaintenance = false,
        Func<ApplicationDbContext, CancellationToken, Task>? beforeDelete = null,
        Func<ApplicationDbContext, CancellationToken, Task>? afterDelete = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        if (beforeDelete is not null) await beforeDelete(context, cancellationToken);

        var targetAssetIds = (assetIds ?? []).ToHashSet();
        if (allAssets)
        {
            targetAssetIds.UnionWith(await context.Assets.Select(asset => asset.Id).ToListAsync(cancellationToken));
        }
        var targetScheduleIds = (scheduleIds ?? []).ToHashSet();
        var targetInspectionIds = (inspectionIds ?? []).ToHashSet();
        var targetFormIds = (formIds ?? []).ToHashSet();
        if (allMaintenance)
        {
            targetScheduleIds.UnionWith(await context.PreventiveMaintenanceSchedules.Select(row => row.Id).ToListAsync(cancellationToken));
            targetInspectionIds.UnionWith(await context.InspectionRecords.Select(row => row.Id).ToListAsync(cancellationToken));
            targetFormIds.UnionWith(await context.PreventiveMaintenanceForms.Select(row => row.Id).ToListAsync(cancellationToken));
        }
        if (targetAssetIds.Count > 0)
        {
            targetScheduleIds.UnionWith(await context.PreventiveMaintenanceSchedules
                .Where(row => targetAssetIds.Contains(row.AssetId)).Select(row => row.Id).ToListAsync(cancellationToken));
            targetInspectionIds.UnionWith(await context.InspectionRecords
                .Where(row => targetAssetIds.Contains(row.AssetId)).Select(row => row.Id).ToListAsync(cancellationToken));
        }
        if (targetScheduleIds.Count > 0)
        {
            targetInspectionIds.UnionWith(await context.InspectionRecords
                .Where(row => targetScheduleIds.Contains(row.ScheduleId)).Select(row => row.Id).ToListAsync(cancellationToken));
        }
        if (targetFormIds.Count > 0)
        {
            targetInspectionIds.UnionWith(await context.InspectionRecords
                .Where(row => row.PreventiveMaintenanceFormId.HasValue
                    && targetFormIds.Contains(row.PreventiveMaintenanceFormId.Value))
                .Select(row => row.Id).ToListAsync(cancellationToken));
        }

        var inspections = await context.InspectionRecords
            .Where(row => targetInspectionIds.Contains(row.Id)).ToListAsync(cancellationToken);
        var actualInspectionIds = inspections.Select(row => row.Id).ToHashSet();
        var impactedFormIds = inspections.Where(row => row.PreventiveMaintenanceFormId.HasValue)
            .Select(row => row.PreventiveMaintenanceFormId!.Value).ToHashSet();
        impactedFormIds.UnionWith(targetFormIds);
        var forms = await context.PreventiveMaintenanceForms
            .Where(form => impactedFormIds.Contains(form.Id)).ToListAsync(cancellationToken);
        var acknowledgements = await context.PreventiveMaintenanceAcknowledgements
            .Where(row => impactedFormIds.Contains(row.FormId)).ToListAsync(cancellationToken);

        var photoRows = await context.InspectionPhotoEvidence
            .Where(row => targetInspectionIds.Contains(row.InspectionId)).ToListAsync(cancellationToken);
        var wmsAudits = await context.InspectionWmsReferralAudits
            .Where(row => targetInspectionIds.Contains(row.InspectionId)).ToListAsync(cancellationToken);
        var referrals = await context.InspectionWmsReferrals
            .Where(row => targetInspectionIds.Contains(row.InspectionId)).ToListAsync(cancellationToken);
        var linkedAttemptIds = inspections.Where(inspection => inspection.LocationAttemptId.HasValue)
            .Select(inspection => inspection.LocationAttemptId!.Value).Distinct().ToArray();
        var locationAttempts = await context.InspectionLocationAttempts
            .Where(row => targetAssetIds.Contains(row.AssetId) || targetScheduleIds.Contains(row.ScheduleId)
                || linkedAttemptIds.Contains(row.Id))
            .ToListAsync(cancellationToken);
        var deletedAttemptIds = locationAttempts.Select(row => row.Id).ToArray();
        var otherAttemptReferences = await context.InspectionRecords
            .Where(row => !targetInspectionIds.Contains(row.Id) && row.LocationAttemptId.HasValue
                && deletedAttemptIds.Contains(row.LocationAttemptId.Value))
            .ToListAsync(cancellationToken);
        foreach (var inspection in otherAttemptReferences) inspection.LocationAttemptId = null;

        var schedulesToDelete = await context.PreventiveMaintenanceSchedules
            .Where(row => targetScheduleIds.Contains(row.Id)).ToListAsync(cancellationToken);
        var schedulesToReconcile = targetScheduleIds
            .Concat(inspections.Select(inspection => inspection.ScheduleId))
            .Distinct().ToArray();
        var assignmentAudits = await context.ScheduleAssignmentReassignmentAudits
            .Where(row => targetScheduleIds.Contains(row.ScheduleId)).ToListAsync(cancellationToken);
        var deferrals = await context.ScheduleEnrollmentDeferrals
            .Where(row => targetAssetIds.Contains(row.AssetId)).ToListAsync(cancellationToken);

        var formsToDelete = new HashSet<Guid>(targetFormIds);
        foreach (var form in forms.Where(form => !formsToDelete.Contains(form.Id)))
        {
            var hasSurvivingRows = await context.InspectionRecords.AnyAsync(row =>
                row.PreventiveMaintenanceFormId == form.Id && !targetInspectionIds.Contains(row.Id), cancellationToken);
            if (!hasSurvivingRows) formsToDelete.Add(form.Id);
        }
        var formsToRetain = forms.Where(form => !formsToDelete.Contains(form.Id)).ToArray();
        foreach (var form in formsToRetain)
        {
            if (form.Status == PreventiveMaintenanceFormStatusCatalog.Acknowledged)
                form.Status = PreventiveMaintenanceFormStatusCatalog.Submitted;
            else if (form.Status == PreventiveMaintenanceFormStatusCatalog.Submitted)
            {
                form.Status = PreventiveMaintenanceFormStatusCatalog.Draft;
                form.SubmittedAt = null;
                form.SubmittedByUserId = null;
            }
            form.UpdatedAt = timeProvider.GetUtcNow();
        }
        var formsRemoved = forms.Count(form => formsToDelete.Contains(form.Id));
        var removedAcknowledgements = acknowledgements.Count;

        var remainingInspectionIds = await context.InspectionRecords
            .Where(row => schedulesToReconcile.Contains(row.ScheduleId) && !targetInspectionIds.Contains(row.Id))
            .Select(row => row.ScheduleId).ToHashSetAsync(cancellationToken);
        var schedulesToReconcileRows = await context.PreventiveMaintenanceSchedules
            .Where(row => schedulesToReconcile.Contains(row.Id)).ToListAsync(cancellationToken);
        var institutionalNow = PreventiveMaintenanceCycle.ToInstitutionalTime(timeProvider.GetUtcNow());
        foreach (var schedule in schedulesToReconcileRows.Where(schedule => !targetScheduleIds.Contains(schedule.Id)
                     && !remainingInspectionIds.Contains(schedule.Id)
                     && schedule.Status == ScheduleStatusCatalog.Completed))
        {
            schedule.Status = PreventiveMaintenanceCycle.ToInstitutionalTime(schedule.ScheduleDate) < institutionalNow
                ? ScheduleStatusCatalog.Overdue
                : ScheduleStatusCatalog.Due;
            schedule.CompletedAt = null;
            schedule.UpdatedAt = timeProvider.GetUtcNow();
        }

        context.InspectionPhotoEvidence.RemoveRange(photoRows);
        context.InspectionWmsReferralAudits.RemoveRange(wmsAudits);
        context.InspectionWmsReferrals.RemoveRange(referrals);
        context.PreventiveMaintenanceAcknowledgements.RemoveRange(acknowledgements);
        context.InspectionRecords.RemoveRange(inspections);
        context.InspectionLocationAttempts.RemoveRange(locationAttempts);
        context.PreventiveMaintenanceForms.RemoveRange(forms.Where(form => formsToDelete.Contains(form.Id)));
        context.ScheduleAssignmentReassignmentAudits.RemoveRange(assignmentAudits);
        context.ScheduleEnrollmentDeferrals.RemoveRange(deferrals);
        context.PreventiveMaintenanceSchedules.RemoveRange(schedulesToDelete);
        List<Asset> assets = deleteAssets
            ? await context.Assets.Where(asset => targetAssetIds.Contains(asset.Id)).ToListAsync(cancellationToken)
            : [];
        context.Assets.RemoveRange(assets);

        await context.SaveChangesAsync(cancellationToken);
        if (afterDelete is not null) await afterDelete(context, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        var filesRemoved = 0;
        var fileFailures = 0;
        foreach (var photo in photoRows)
        {
            try
            {
                await photoStorage.DeleteAsync(photo.StorageKey, CancellationToken.None);
                filesRemoved++;
            }
            catch (Exception exception)
            {
                fileFailures++;
                logger.LogError(exception, "Admin record cleanup could not remove an inspection photo file.");
            }
        }

        return new AdminDeleteResult(
            assets.Count,
            schedulesToDelete.Count,
            inspections.Count,
            formsRemoved,
            removedAcknowledgements,
            filesRemoved,
            fileFailures);
    }
}

internal sealed class AdminDemoDataConflictException(string message) : Exception(message);

public sealed record AdminOverviewResponse(
    int ActiveAccounts,
    int InactiveAccounts,
    int Assets,
    int Schedules,
    int Inspections,
    int DraftForms,
    int SubmittedForms,
    int AcknowledgedForms,
    int Acknowledgements);

public sealed record AdminAssetRow(
    Guid Id, string AssetCode, string AssetCategory, string? Building, string? Department,
    string? Location, string? QrCodeValue, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AdminAssetPageResponse(int TotalCount, int Page, int PageSize, IReadOnlyList<AdminAssetRow> Items);

public sealed record AdminScheduleRow(
    Guid Id, Guid AssetId, string AssetCode, string AssetCategory, string? Department,
    DateTimeOffset ScheduleDate, string PmCycle, string PeriodType, string Status,
    string? Quarter, string? Semester, int? Year, string? AcademicYear,
    Guid? InspectorUserId, string? InspectorDisplayName,
    Guid? SupervisorUserId, string? SupervisorDisplayName, DateTimeOffset? CompletedAt);
public sealed record AdminSchedulePageResponse(int TotalCount, int Page, int PageSize, IReadOnlyList<AdminScheduleRow> Items);

public sealed record AdminInspectionRow(
    Guid Id, Guid ScheduleId, Guid AssetId, string AssetCode, DateTimeOffset DateInspected,
    Guid InspectorUserId, string? InspectorDisplayName, bool IsOperational,
    Guid? FormId, string? FormStatus, string? FormFileNumber);
public sealed record AdminInspectionPageResponse(int TotalCount, int Page, int PageSize, IReadOnlyList<AdminInspectionRow> Items);

public sealed record AdminFormRow(
    Guid Id, string? FileNumber, string AssetCategory, string? Building, string? Department,
    string? PmCycle, string Status, int InspectionCount, bool HasAcknowledgement, DateTimeOffset UpdatedAt);
public sealed record AdminFormPageResponse(int TotalCount, int Page, int PageSize, IReadOnlyList<AdminFormRow> Items);

public sealed record AdminDeleteResult(
    int AssetsRemoved = 0,
    int SchedulesRemoved = 0,
    int InspectionsRemoved = 0,
    int FormsRemoved = 0,
    int AcknowledgementsRemoved = 0,
    int PhotoFilesRemoved = 0,
    int PhotoFileCleanupFailures = 0);

public sealed record AdminValidationDatasetResponse(
    AdminDeleteResult Deleted,
    int AssetsSeeded,
    int SchedulesSeeded,
    int InspectionsSeeded,
    int FormsSeeded,
    int AcknowledgementsSeeded,
    int WmsReferralsSeeded,
    int WmsReferralAuditsSeeded);
