using System.Data;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.AdminManagement;

public static class AdminOperationalEndpoints
{
    public static IEndpointRouteBuilder MapAdminOperationalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin")
            .WithTags("Admin demonstration management")
            .RequireAuthorization(AuthPolicyCatalog.CanManageAdminManagement);

        group.MapGet("/overview", async (
            AdminOperationalDataService service,
            CancellationToken cancellationToken) =>
                Results.Ok(await service.GetOverviewAsync(cancellationToken)))
            .WithName("GetAdminOverview")
            .Produces<AdminOverviewResponse>();

        group.MapGet("/assets", ListAssetsAsync)
            .WithName("ListAdminAssets")
            .Produces<AdminAssetPageResponse>()
            .ProducesValidationProblem();
        group.MapPost("/assets", CreateAssetAsync)
            .WithName("CreateAdminAsset")
            .Produces<AdminAssetRow>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/assets/{id:guid}", UpdateAssetAsync)
            .WithName("UpdateAdminAsset")
            .Produces<AdminAssetRow>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/assets/{id:guid}", DeleteAssetAsync)
            .WithName("DeleteAdminAsset")
            .Produces<AdminDeleteResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/assets/bulk-delete", DeleteAssetsAsync)
            .WithName("BulkDeleteAdminAssets")
            .Produces<AdminDeleteResult>()
            .ProducesValidationProblem();
        group.MapDelete("/assets/all", ClearAssetsAsync)
            .WithName("ClearAdminAssets")
            .Produces<AdminDeleteResult>();

        group.MapGet("/maintenance/schedules", ListSchedulesAsync)
            .WithName("ListAdminSchedules")
            .Produces<AdminSchedulePageResponse>()
            .ProducesValidationProblem();
        group.MapPut("/maintenance/schedules/{id:guid}", UpdateScheduleAsync)
            .WithName("UpdateAdminSchedule")
            .Produces<ScheduleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/maintenance/schedules/{id:guid}", DeleteScheduleAsync)
            .WithName("DeleteAdminSchedule")
            .Produces<AdminDeleteResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/maintenance/schedules/bulk-delete", DeleteSchedulesAsync)
            .WithName("BulkDeleteAdminSchedules")
            .Produces<AdminDeleteResult>()
            .ProducesValidationProblem();

        group.MapGet("/maintenance/inspections", ListInspectionsAsync)
            .WithName("ListAdminInspections")
            .Produces<AdminInspectionPageResponse>()
            .ProducesValidationProblem();
        group.MapDelete("/maintenance/inspections/{id:guid}", DeleteInspectionAsync)
            .WithName("DeleteAdminInspection")
            .Produces<AdminDeleteResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/maintenance/inspections/bulk-delete", DeleteInspectionsAsync)
            .WithName("BulkDeleteAdminInspections")
            .Produces<AdminDeleteResult>()
            .ProducesValidationProblem();

        group.MapGet("/maintenance/forms", ListFormsAsync)
            .WithName("ListAdminForms")
            .Produces<AdminFormPageResponse>()
            .ProducesValidationProblem();
        group.MapPut("/maintenance/forms/{id:guid}", UpdateFormAsync)
            .WithName("UpdateAdminForm")
            .Produces<AdminFormRow>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapDelete("/maintenance/forms/{id:guid}", DeleteFormAsync)
            .WithName("DeleteAdminForm")
            .Produces<AdminDeleteResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete("/maintenance/forms/{id:guid}/acknowledgement", DeleteAcknowledgementAsync)
            .WithName("DeleteAdminAcknowledgement")
            .Produces<AdminDeleteResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/maintenance/forms/{id:guid}/return-to-draft", ReturnFormToDraftAsync)
            .WithName("ReturnAdminFormToDraft")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/demo-data/maintenance", ClearMaintenanceAsync)
            .WithName("ClearAdminMaintenanceData")
            .Produces<AdminDeleteResult>();
        group.MapDelete("/demo-data/assets", ClearAllDemoDataAsync)
            .WithName("ClearAdminAssetsAndMaintenance")
            .Produces<AdminDeleteResult>();

        return endpoints;
    }

    private static async Task<IResult> ListAssetsAsync(
        string? search, string? category, string? status, int? page, int? pageSize,
        AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var errors = ValidatePage(page, pageSize);
        if (search?.Trim().Length > 256)
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(category) && !AssetCategoryCatalog.TryNormalize(category, out _))
            errors[nameof(category)] = ["Choose a supported asset category."];
        if (!string.IsNullOrWhiteSpace(status) && !AssetStatusCatalog.TryNormalize(status, out _))
            errors[nameof(status)] = ["Choose a supported asset status."];
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.ListAssetsAsync(search, category, status,
            page ?? 1, pageSize ?? 25, cancellationToken));
    }

    private static async Task<IResult> CreateAssetAsync(
        CreateAssetDto request,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        PreventiveMaintenanceScheduleGenerationService scheduleGenerator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        AssetCategoryCatalog.TryNormalize(request.AssetCategory, out var category);
        var code = AssetCodeValue.Normalize(request.AssetCode);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        if (await context.Assets.AnyAsync(asset => asset.AssetCode == code, cancellationToken))
            return ApiErrors.Conflict("An asset with that code already exists.");
        var now = timeProvider.GetUtcNow();
        var asset = new Asset
        {
            Id = Guid.NewGuid(), AssetCode = code, AssetCategory = category,
            Building = Normalize(request.Building), Department = Normalize(request.Department),
            Location = Normalize(request.Location), Status = AssetStatusCatalog.Active,
            VerificationLatitude = request.VerificationLatitude,
            VerificationLongitude = request.VerificationLongitude,
            VerificationRadiusMeters = request.VerificationRadiusMeters,
            CreatedAt = now, UpdatedAt = now
        };
        asset.QrCodeValue = AssetQrCodeValue.Create(asset.AssetCategory, asset.Id);
        context.Assets.Add(asset);
        try
        {
            await scheduleGenerator.AddCurrentYearUpcomingCyclesAsync(context, asset, now,
                cancellationToken, transaction);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
        {
            return ApiErrors.Conflict("Asset code or QR value already exists.");
        }
        return Results.Created($"/api/v1/admin/assets/{asset.Id}", ToAdminAssetRow(asset));
    }

    private static async Task<IResult> UpdateAssetAsync(
        Guid id,
        AdminUpdateAssetRequest request,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        AssetCategoryCatalog.TryNormalize(request.AssetCategory, out var category);
        AssetStatusCatalog.TryNormalize(request.Status, out var status);
        var code = AssetCodeValue.Normalize(request.AssetCode);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var asset = await context.Assets.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (asset is null) return ApiErrors.NotFound("Asset not found.");
        if (await context.Assets.AnyAsync(candidate => candidate.Id != id && candidate.AssetCode == code, cancellationToken))
            return ApiErrors.Conflict("An asset with that code already exists.");
        if (!string.Equals(asset.AssetCategory, category, StringComparison.Ordinal))
        {
            var schedules = await context.PreventiveMaintenanceSchedules
                .Where(schedule => schedule.AssetId == id).Select(schedule => schedule.PmCycle)
                .ToListAsync(cancellationToken);
            if (schedules.Any(cycle => !PreventiveMaintenanceCycle.TryParse(cycle, out _, out var month)
                    || !CpmpScheduleFrequency.IsValid(category, month)))
                return ApiErrors.Conflict("The asset category cannot change while it has schedules outside the category's CPMP frequency.");
        }
        asset.AssetCode = code;
        asset.AssetCategory = category;
        asset.Building = Normalize(request.Building);
        asset.Department = Normalize(request.Department);
        asset.Location = Normalize(request.Location);
        asset.Status = status;
        asset.QrCodeValue = AssetQrCodeValue.Create(category, asset.Id);
        asset.VerificationLatitude = request.VerificationLatitude;
        asset.VerificationLongitude = request.VerificationLongitude;
        asset.VerificationRadiusMeters = request.VerificationRadiusMeters;
        asset.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
        {
            return ApiErrors.Conflict("Asset code or QR value already exists.");
        }
        return Results.Ok(ToAdminAssetRow(asset));
    }

    private static async Task<IResult> DeleteAssetAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAssetAsync(id, cancellationToken);
        return result.AssetsRemoved == 0
            ? ApiErrors.NotFound("Asset not found.")
            : Results.Ok(result);
    }

    private static async Task<IResult> DeleteAssetsAsync(
        AdminBulkDeleteRequest request, AdminOperationalDataService service,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.DeleteAssetsAsync(request.Ids, false, cancellationToken));
    }

    private static async Task<IResult> ClearAssetsAsync(
        AdminOperationalDataService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ClearAssetsAndMaintenanceAsync(cancellationToken));

    private static async Task<IResult> ListSchedulesAsync(
        string? search, string? status, int? page, int? pageSize,
        AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var errors = ValidatePage(page, pageSize);
        if (search?.Trim().Length > 256)
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(status) && !ScheduleStatusCatalog.TryNormalize(status, out _))
            errors[nameof(status)] = ["Choose a supported schedule status."];
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.ListSchedulesAsync(search, status,
            page ?? 1, pageSize ?? 25, cancellationToken));
    }

    private static async Task<IResult> UpdateScheduleAsync(
        Guid id,
        AdminUpdateScheduleRequest request,
        ClaimsPrincipal principal,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        ScheduleStatusCatalog.TryNormalize(request.Status, out var status);
        SchedulePeriodTypeCatalog.TryNormalize(request.PeriodType, out var periodType);
        var cycle = PreventiveMaintenanceCycle.FromScheduleDate(request.ScheduleDate);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginTransactionAsync(context, cancellationToken);
        var schedule = await context.PreventiveMaintenanceSchedules
            .Include(candidate => candidate.Asset).SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (schedule?.Asset is null) return ApiErrors.NotFound("Schedule not found.");
        if (schedule.AssetId != request.AssetId)
        {
            if (await context.InspectionRecords.AnyAsync(inspection => inspection.ScheduleId == id, cancellationToken))
                return ApiErrors.Conflict("A schedule with inspection records cannot be moved to another asset.");
            var newAsset = await context.Assets.SingleOrDefaultAsync(asset => asset.Id == request.AssetId, cancellationToken);
            if (newAsset is null) return ApiErrors.NotFound("Asset not found.");
            schedule.Asset = newAsset;
            schedule.AssetId = newAsset.Id;
        }
        if (!CpmpScheduleFrequency.IsValid(schedule.Asset.AssetCategory,
                DateOnly.FromDateTime(request.ScheduleDate.DateTime).Month))
            return ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ScheduleDate)] = ["The schedule date must follow the asset category's existing CPMP frequency."]
            });
        if (await context.PreventiveMaintenanceSchedules.AnyAsync(candidate => candidate.Id != id
                && candidate.AssetId == schedule.AssetId && candidate.PmCycle == cycle, cancellationToken))
            return ApiErrors.Conflict("A schedule already exists for this asset and PM cycle.");
        var inspectionExists = await context.InspectionRecords.AnyAsync(inspection => inspection.ScheduleId == id, cancellationToken);
        if (status == ScheduleStatusCatalog.Completed && !inspectionExists)
            return ApiErrors.Conflict("A schedule cannot be marked completed without its inspection record.");
        if (request.AssignedToUserId is Guid inspectorId)
        {
            var inspector = await userManager.FindByIdAsync(inspectorId.ToString());
            if (inspector is null || !inspector.IsActive || !await userManager.IsInRoleAsync(inspector, AuthRoleCatalog.Inspector))
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.AssignedToUserId)] = ["Choose an active Inspector account."]
                });
        }
        if (request.AssignedSupervisorUserId is Guid supervisorId)
        {
            var supervisor = await userManager.FindByIdAsync(supervisorId.ToString());
            if (supervisor is null || !supervisor.IsActive || !await userManager.IsInRoleAsync(supervisor, AuthRoleCatalog.Supervisor))
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.AssignedSupervisorUserId)] = ["Choose an active Supervisor account."]
                });
        }

        var actorId = Guid.TryParse(principal.FindFirst("sub")?.Value, out var parsedActor) ? parsedActor : Guid.Empty;
        AddAssignmentAudit(context, schedule, "Inspector", schedule.AssignedToUserId, request.AssignedToUserId, actorId, timeProvider.GetUtcNow());
        AddAssignmentAudit(context, schedule, "Supervisor", schedule.AssignedSupervisorUserId, request.AssignedSupervisorUserId, actorId, timeProvider.GetUtcNow());
        schedule.ScheduleDate = request.ScheduleDate;
        schedule.PmCycle = cycle;
        schedule.PeriodType = periodType;
        schedule.Quarter = periodType == SchedulePeriodTypeCatalog.Quarter
            ? ScheduleQuarterCatalog.TryNormalizeNullable(request.Quarter, out var normalizedQuarter) ? normalizedQuarter : null
            : null;
        schedule.Semester = periodType == SchedulePeriodTypeCatalog.Semester ? Normalize(request.Semester) : null;
        schedule.Year = request.ScheduleDate.Year;
        schedule.AcademicYear = Normalize(request.AcademicYear);
        schedule.Status = status;
        schedule.AssignedToUserId = request.AssignedToUserId;
        schedule.AssignedSupervisorUserId = request.AssignedSupervisorUserId;
        schedule.CompletedAt = status == ScheduleStatusCatalog.Completed
            ? schedule.CompletedAt ?? timeProvider.GetUtcNow()
            : null;
        schedule.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Results.Ok(ScheduleResponse.FromSchedule(schedule));
    }

    private static async Task<IResult> DeleteScheduleAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteSchedulesAsync([id], cancellationToken);
        return result.SchedulesRemoved == 0 ? ApiErrors.NotFound("Schedule not found.") : Results.Ok(result);
    }

    private static async Task<IResult> DeleteSchedulesAsync(
        AdminBulkDeleteRequest request, AdminOperationalDataService service,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.DeleteSchedulesAsync(request.Ids, cancellationToken));
    }

    private static async Task<IResult> ListInspectionsAsync(
        string? search, string? formStatus, int? page, int? pageSize,
        AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var errors = ValidatePage(page, pageSize);
        if (search?.Trim().Length > 256)
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(formStatus)
            && !PreventiveMaintenanceFormStatusCatalog.TryNormalize(formStatus, out _))
            errors[nameof(formStatus)] = ["Choose a supported form status."];
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.ListInspectionsAsync(search, formStatus,
            page ?? 1, pageSize ?? 25, cancellationToken));
    }

    private static async Task<IResult> DeleteInspectionAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteInspectionsAsync([id], cancellationToken);
        return result.InspectionsRemoved == 0 ? ApiErrors.NotFound("Inspection not found.") : Results.Ok(result);
    }

    private static async Task<IResult> DeleteInspectionsAsync(
        AdminBulkDeleteRequest request, AdminOperationalDataService service,
        CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.DeleteInspectionsAsync(request.Ids, cancellationToken));
    }

    private static async Task<IResult> ListFormsAsync(
        string? search, string? status, int? page, int? pageSize,
        AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var errors = ValidatePage(page, pageSize);
        if (search?.Trim().Length > 256)
            errors[nameof(search)] = ["Search must be 256 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(status)
            && !PreventiveMaintenanceFormStatusCatalog.TryNormalize(status, out _))
            errors[nameof(status)] = ["Choose a supported form status."];
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        return Results.Ok(await service.ListFormsAsync(search, status,
            page ?? 1, pageSize ?? 25, cancellationToken));
    }

    private static async Task<IResult> UpdateFormAsync(
        Guid id, AdminUpdateFormRequest request,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var errors = request.Validate();
        if (errors.Count > 0) return ApiErrors.Validation(errors);
        AssetCategoryCatalog.TryNormalize(request.AssetCategory, out var category);
        SchedulePeriodTypeCatalog.TryNormalize(request.PeriodType, out var periodType);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var form = await context.PreventiveMaintenanceForms.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (form is null) return ApiErrors.NotFound("Form not found.");
        if (form.Status == PreventiveMaintenanceFormStatusCatalog.Acknowledged)
            return ApiErrors.Conflict("Remove the acknowledgement before editing an acknowledged form.");
        form.FileNumber = Normalize(request.FileNumber);
        form.AssetCategory = category;
        form.Building = Normalize(request.Building);
        form.Department = Normalize(request.Department);
        form.PmCycle = Normalize(request.PmCycle);
        form.PeriodType = periodType;
        form.Quarter = ScheduleQuarterCatalog.TryNormalizeNullable(request.Quarter, out var quarter) ? quarter : null;
        form.Semester = Normalize(request.Semester);
        form.Year = request.Year;
        form.AcademicYear = Normalize(request.AcademicYear);
        form.UpdatedAt = timeProvider.GetUtcNow();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
        {
            return ApiErrors.Conflict("The form identifier or batch already exists.");
        }
        return Results.Ok(new AdminFormRow(form.Id, form.FileNumber, form.AssetCategory,
            form.Building, form.Department, form.PmCycle, form.Status, 0, false, form.UpdatedAt));
    }

    private static async Task<IResult> DeleteFormAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteFormAsync(id, cancellationToken);
        return result.FormsRemoved == 0 ? ApiErrors.NotFound("Form not found.") : Results.Ok(result);
    }

    private static async Task<IResult> DeleteAcknowledgementAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAcknowledgementAsync(id, cancellationToken);
        return result.AcknowledgementsRemoved == 0
            ? ApiErrors.NotFound("Acknowledgement not found.")
            : Results.Ok(result);
    }

    private static async Task<IResult> ReturnFormToDraftAsync(
        Guid id, AdminOperationalDataService service, CancellationToken cancellationToken)
    {
        var changed = await service.ReturnFormToDraftAsync(id, cancellationToken);
        return changed ? Results.NoContent()
            : ApiErrors.Conflict("Only Submitted forms can be returned to Draft.");
    }

    private static async Task<IResult> ClearMaintenanceAsync(
        AdminOperationalDataService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ClearMaintenanceAsync(cancellationToken));

    private static async Task<IResult> ClearAllDemoDataAsync(
        AdminOperationalDataService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ClearAssetsAndMaintenanceAsync(cancellationToken));

    private static Dictionary<string, string[]> ValidatePage(int? page, int? pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1) errors[nameof(page)] = ["Page must be greater than zero."];
        if (pageSize is < 1 or > 100) errors[nameof(pageSize)] = ["Page size must be between 1 and 100."];
        return errors;
    }

    private static void AddAssignmentAudit(
        ApplicationDbContext context,
        PreventiveMaintenanceSchedule schedule,
        string role,
        Guid? previous,
        Guid? next,
        Guid actorId,
        DateTimeOffset now)
    {
        if (previous == next || next is null) return;
        context.ScheduleAssignmentReassignmentAudits.Add(new ScheduleAssignmentReassignmentAudit
        {
            Id = Guid.NewGuid(), ScheduleId = schedule.Id, AssignmentRole = role,
            PreviousAssigneeUserId = previous, NewAssigneeUserId = next.Value,
            ChangedByUserId = actorId, ChangedAt = now, Reason = "Admin demonstration record update"
        });
    }

    private static async Task<IDbContextTransaction?> BeginTransactionAsync(
        ApplicationDbContext context, CancellationToken cancellationToken) =>
        context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminAssetRow ToAdminAssetRow(Asset asset) => new(
        asset.Id, asset.AssetCode, asset.AssetCategory, asset.Building, asset.Department,
        asset.Location, asset.QrCodeValue, asset.Status, asset.CreatedAt, asset.UpdatedAt);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminBulkDeleteRequest(IReadOnlyCollection<Guid> Ids)
{
    internal Dictionary<string, string[]> Validate()
    {
        if (Ids is { Count: > 0 and <= 500 } && Ids.All(id => id != Guid.Empty)
            && Ids.Distinct().Count() == Ids.Count) return [];
        return new Dictionary<string, string[]>
        {
            [nameof(Ids)] = ["Select between 1 and 500 unique records."]
        };
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateAssetRequest(
    string AssetCode, string AssetCategory, string Status, string? Building,
    string? Department, string? Location, double? VerificationLatitude,
    double? VerificationLongitude, double? VerificationRadiusMeters)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(AssetCode) || AssetCode.Trim().Length > AssetCodeValue.MaxLength)
            errors[nameof(AssetCode)] = ["Asset code is required and must not exceed 64 characters."];
        if (!AssetCategoryCatalog.TryNormalize(AssetCategory, out _))
            errors[nameof(AssetCategory)] = ["Choose a supported asset category."];
        if (!AssetStatusCatalog.TryNormalize(Status, out _))
            errors[nameof(Status)] = ["Choose a supported asset status."];
        if (Building?.Trim().Length > 256) errors[nameof(Building)] = ["Building must not exceed 256 characters."];
        if (Department?.Trim().Length > 256) errors[nameof(Department)] = ["Department must not exceed 256 characters."];
        if (Location?.Trim().Length > 256) errors[nameof(Location)] = ["Location must not exceed 256 characters."];
        AssetVerificationLocationRules.AddConfigurationErrors(
            VerificationLatitude, VerificationLongitude, VerificationRadiusMeters, errors);
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateScheduleRequest(
    Guid AssetId, DateTimeOffset ScheduleDate, string PeriodType, string? Quarter,
    string? Semester, string? AcademicYear, string Status,
    Guid? AssignedToUserId, Guid? AssignedSupervisorUserId)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (AssetId == Guid.Empty) errors[nameof(AssetId)] = ["Asset is required."];
        if (ScheduleDate == default) errors[nameof(ScheduleDate)] = ["Schedule date is required."];
        if (!SchedulePeriodTypeCatalog.TryNormalize(PeriodType, out _))
            errors[nameof(PeriodType)] = ["Choose a supported period type."];
        if (!ScheduleStatusCatalog.TryNormalize(Status, out _))
            errors[nameof(Status)] = ["Choose a supported schedule status."];
        if (!ScheduleQuarterCatalog.TryNormalizeNullable(Quarter, out _))
            errors[nameof(Quarter)] = ["Quarter must be Q1, Q2, Q3, or Q4."];
        if (Semester?.Trim().Length > 16) errors[nameof(Semester)] = ["Semester must not exceed 16 characters."];
        if (AcademicYear?.Trim().Length > 16) errors[nameof(AcademicYear)] = ["Academic year must not exceed 16 characters."];
        return errors;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminUpdateFormRequest(
    string? FileNumber, string AssetCategory, string? Building, string? Department,
    string? PmCycle, string PeriodType, string? Quarter, string? Semester,
    int? Year, string? AcademicYear)
{
    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (FileNumber?.Trim().Length > 64) errors[nameof(FileNumber)] = ["Form number must not exceed 64 characters."];
        if (!AssetCategoryCatalog.TryNormalize(AssetCategory, out _))
            errors[nameof(AssetCategory)] = ["Choose a supported asset category."];
        if (!SchedulePeriodTypeCatalog.TryNormalize(PeriodType, out _))
            errors[nameof(PeriodType)] = ["Choose a supported period type."];
        if (!ScheduleQuarterCatalog.TryNormalizeNullable(Quarter, out _))
            errors[nameof(Quarter)] = ["Quarter must be Q1, Q2, Q3, or Q4."];
        if (PmCycle is not null && !string.IsNullOrWhiteSpace(PmCycle)
            && !PreventiveMaintenanceCycle.TryParse(PmCycle, out _, out _))
            errors[nameof(PmCycle)] = ["PM cycle must use yyyy-MM format."];
        foreach (var (value, field) in new[]
        {
            (Building, nameof(Building)), (Department, nameof(Department)),
            (Semester, nameof(Semester)), (AcademicYear, nameof(AcademicYear))
        })
            if (value?.Trim().Length > 256) errors[field] = ["Value must not exceed 256 characters."];
        return errors;
    }
}
