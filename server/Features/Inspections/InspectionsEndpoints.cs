using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionsEndpoints
{
    public static IEndpointRouteBuilder MapInspectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inspections").WithTags("Inspections");

        group.MapGet("/history/{assetId}", async (
            Guid assetId,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var history = await context.InspectionRecords
                .WhereOfficial()
                .Where(i => i.AssetId == assetId)
                .OrderByDescending(i => i.DateInspected)
                .Select(i => new InspectionHistoryResponse(
                    i.Id,
                    i.DateInspected,
                    i.IsOperational,
                    i.Remarks,
                    i.ActionsRecommendations,
                    i.DateAccomplished,
                    i.WaterReplaceCarbonFilter,
                    i.WaterReplaceSedimentFilter,
                    i.WaterCheckUvLight))
                .ToListAsync(cancellationToken);

            return Results.Ok(history);
        })
        .WithName("GetInspectionHistory")
        .WithSummary("Gets inspection history for an asset")
        .Produces<List<InspectionHistoryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id}/wms-referral", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .AsNoTracking()
                .Include(candidate => candidate.PreventiveMaintenanceForm)
                .Include(candidate => candidate.WmsReferral)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (inspection is null)
            {
                return ApiErrors.NotFound("Inspection not found.");
            }

            var hasAcknowledgement = inspection.PreventiveMaintenanceFormId is { } formId
                && await context.PreventiveMaintenanceAcknowledgements
                    .AsNoTracking()
                    .AnyAsync(acknowledgement => acknowledgement.FormId == formId, cancellationToken);
            var audit = await context.InspectionWmsReferralAudits
                .AsNoTracking()
                .Where(entry => entry.InspectionId == inspection.Id)
                .OrderBy(entry => entry.Revision)
                .Select(entry => new InspectionWmsReferralAuditResponse(
                    entry.PreviousExternalPmNumber,
                    entry.NewExternalPmNumber,
                    entry.Revision,
                    entry.ChangedByUserId,
                    entry.ChangedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(InspectionWmsReferralDetailResponse.FromInspection(
                inspection,
                hasAcknowledgement,
                audit));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManageWmsReferral)
        .WithName("GetInspectionWmsReferral")
        .WithSummary("Gets the manually recorded WMS PM number and its change audit")
        .Produces<InspectionWmsReferralDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id}/wms-referral", async (
            Guid id,
            UpdateInspectionWmsReferralDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorUserId))
            {
                return ApiErrors.Unauthorized("The authenticated user is unavailable.");
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .Include(candidate => candidate.PreventiveMaintenanceForm)
                .Include(candidate => candidate.WmsReferral)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (inspection is null)
            {
                return ApiErrors.NotFound("Inspection not found.");
            }

            var hasAcknowledgement = inspection.PreventiveMaintenanceFormId is { } formId
                && await context.PreventiveMaintenanceAcknowledgements
                    .AsNoTracking()
                    .AnyAsync(acknowledgement => acknowledgement.FormId == formId, cancellationToken);
            var eligibilityError = InspectionWmsReferralDetailResponse.EligibilityError(
                inspection,
                hasAcknowledgement);
            if (eligibilityError is not null)
            {
                return ApiErrors.Conflict(eligibilityError);
            }

            var normalizedNumber = dto.ExternalPmNumber.Trim();
            var existing = inspection.WmsReferral;
            if (existing is null)
            {
                if (dto.ExpectedRevision != 0)
                {
                    return ApiErrors.Conflict("The WMS referral changed. Refresh the inspection and try again.");
                }

                var now = DateTimeOffset.UtcNow;
                var referral = new InspectionWmsReferral
                {
                    InspectionId = inspection.Id,
                    ExternalPmNumber = normalizedNumber,
                    Revision = 1,
                    RecordedByUserId = actorUserId,
                    RecordedAt = now,
                    LastUpdatedByUserId = actorUserId,
                    LastUpdatedAt = now
                };
                context.InspectionWmsReferrals.Add(referral);
                context.InspectionWmsReferralAudits.Add(new InspectionWmsReferralAudit
                {
                    Id = Guid.NewGuid(),
                    InspectionId = inspection.Id,
                    PreviousExternalPmNumber = null,
                    NewExternalPmNumber = normalizedNumber,
                    Revision = 1,
                    ChangedByUserId = actorUserId,
                    ChangedAt = now
                });

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return ApiErrors.Conflict("The WMS referral changed. Refresh the inspection and try again.");
                }
                catch (DbUpdateException exception) when (
                    DatabaseConstraintViolation.IsUniqueConstraint(exception, "PK_InspectionWmsReferrals")
                    || DatabaseConstraintViolation.IsUniqueConstraint(
                        exception,
                        "IX_InspectionWmsReferralAudits_InspectionId_Revision"))
                {
                    return ApiErrors.Conflict("A WMS referral was recorded for this inspection. Refresh it before making another change.");
                }

                return Results.Ok(InspectionWmsReferralResponse.FromReferral(referral));
            }

            if (dto.ExpectedRevision != existing.Revision)
            {
                return ApiErrors.Conflict("The WMS referral changed. Refresh the inspection and try again.");
            }

            if (string.Equals(existing.ExternalPmNumber, normalizedNumber, StringComparison.Ordinal))
            {
                return Results.Ok(InspectionWmsReferralResponse.FromReferral(existing));
            }

            var changedAt = DateTimeOffset.UtcNow;
            var previousNumber = existing.ExternalPmNumber;
            existing.ExternalPmNumber = normalizedNumber;
            existing.Revision++;
            existing.LastUpdatedByUserId = actorUserId;
            existing.LastUpdatedAt = changedAt;
            context.InspectionWmsReferralAudits.Add(new InspectionWmsReferralAudit
            {
                Id = Guid.NewGuid(),
                InspectionId = inspection.Id,
                PreviousExternalPmNumber = previousNumber,
                NewExternalPmNumber = normalizedNumber,
                Revision = existing.Revision,
                ChangedByUserId = actorUserId,
                ChangedAt = changedAt
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiErrors.Conflict("The WMS referral changed. Refresh the inspection and try again.");
            }
            catch (DbUpdateException exception) when (
                DatabaseConstraintViolation.IsUniqueConstraint(exception, "PK_InspectionWmsReferrals")
                || DatabaseConstraintViolation.IsUniqueConstraint(
                    exception,
                    "IX_InspectionWmsReferralAudits_InspectionId_Revision"))
            {
                return ApiErrors.Conflict("The WMS referral changed. Refresh it before making another correction.");
            }

            return Results.Ok(InspectionWmsReferralResponse.FromReferral(existing));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManageWmsReferral)
        .WithName("UpdateInspectionWmsReferral")
        .WithSummary("Records or corrects the external WMS PM number without changing the inspection")
        .Produces<InspectionWmsReferralResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
            Guid? assetId,
            Guid? scheduleId,
            bool? isOperational,
            DateTimeOffset? dateFrom,
            DateTimeOffset? dateTo,
            string? department,
            string? assetCategory,
            string? search,
            string? wmsReferralStatus,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (dateFrom is not null && dateTo is not null && dateFrom > dateTo)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dateFrom)] = ["Date from must be earlier than or equal to date to."]
                });
            }

            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim();
            var normalizedCategory = string.Empty;
            var normalizedReferralStatus = string.Empty;
            var validationErrors = new Dictionary<string, string[]>();
            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (normalizedSearch?.Length > 256)
            {
                validationErrors[nameof(search)] = ["Search must be 256 characters or fewer."];
            }

            if (!string.IsNullOrWhiteSpace(assetCategory)
                && !AssetCategoryCatalog.TryNormalize(assetCategory, out normalizedCategory))
            {
                validationErrors[nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."];
            }

            if (!string.IsNullOrWhiteSpace(wmsReferralStatus)
                && !InspectionFollowUpStatusCatalog.TryNormalize(wmsReferralStatus, out normalizedReferralStatus))
            {
                validationErrors[nameof(wmsReferralStatus)] = ["WMS referral status must be NoReferralRequired, CorrectiveFollowUpPending, or ReferredToWms."];
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.InspectionRecords
                .AsNoTracking()
                .WhereOfficial()
                .AsQueryable();

            if (assetId is not null)
            {
                query = query.Where(inspection => inspection.AssetId == assetId.Value);
            }

            if (scheduleId is not null)
            {
                query = query.Where(inspection => inspection.ScheduleId == scheduleId.Value);
            }

            if (isOperational is not null)
            {
                query = query.Where(inspection => inspection.IsOperational == isOperational.Value);
            }

            if (dateFrom is not null)
            {
                query = query.Where(inspection => inspection.DateInspected >= dateFrom.Value);
            }

            if (dateTo is not null)
            {
                query = query.Where(inspection => inspection.DateInspected <= dateTo.Value);
            }

            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(inspection =>
                    inspection.Asset != null
                    && inspection.Asset.Department != null
                    && inspection.Asset.Department.ToUpper() == departmentKey);
            }

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                query = query.Where(inspection =>
                    inspection.Asset != null
                    && inspection.Asset.AssetCategory == normalizedCategory);
            }

            if (normalizedSearch is not null)
            {
                var searchKey = normalizedSearch.ToUpperInvariant();
                query = query.Where(inspection =>
                    (inspection.Asset != null && (
                        inspection.Asset.AssetCode.Contains(searchKey)
                        || inspection.Asset.AssetCategory.ToUpper().Contains(searchKey)
                        || (inspection.Asset.Building != null && inspection.Asset.Building.ToUpper().Contains(searchKey))
                        || (inspection.Asset.Department != null && inspection.Asset.Department.ToUpper().Contains(searchKey))
                        || (inspection.Asset.Location != null && inspection.Asset.Location.ToUpper().Contains(searchKey))))
                    || (inspection.Remarks != null && inspection.Remarks.ToUpper().Contains(searchKey))
                    || (inspection.ActionsRecommendations != null && inspection.ActionsRecommendations.ToUpper().Contains(searchKey))
                    || (inspection.WmsReferral != null && inspection.WmsReferral.ExternalPmNumber.ToUpper().Contains(searchKey)));
            }

            if (!string.IsNullOrWhiteSpace(wmsReferralStatus))
            {
                query = normalizedReferralStatus switch
                {
                    InspectionFollowUpStatusCatalog.NoReferralRequired => query.Where(inspection =>
                        inspection.IsOperational && inspection.WmsReferral == null),
                    InspectionFollowUpStatusCatalog.CorrectiveFollowUpPending => query.Where(inspection =>
                        !inspection.IsOperational && inspection.WmsReferral == null),
                    InspectionFollowUpStatusCatalog.ReferredToWms => query.Where(inspection =>
                        inspection.WmsReferral != null),
                    _ => query
                };
            }

            var inspections = await query
                .OrderByDescending(inspection => inspection.DateInspected)
                .ThenBy(inspection => inspection.Id)
                .Select(inspection => new InspectionResponse(
                    inspection.Id,
                    inspection.ScheduleId,
                    inspection.AssetId,
                    inspection.InspectorUserId,
                    inspection.DateInspected,
                    inspection.IsOperational,
                    inspection.Remarks,
                    inspection.ActionsRecommendations,
                    inspection.CreatedAt,
                    inspection.UpdatedAt,
                    inspection.DateAccomplished,
                    inspection.WaterReplaceCarbonFilter,
                    inspection.WaterReplaceSedimentFilter,
                    inspection.WaterCheckUvLight,
                    inspection.WmsReferral == null ? null : inspection.WmsReferral.ExternalPmNumber,
                    inspection.WmsReferral == null ? 0 : inspection.WmsReferral.Revision,
                    inspection.WmsReferral != null
                        ? InspectionFollowUpStatusCatalog.ReferredToWms
                        : inspection.IsOperational
                            ? InspectionFollowUpStatusCatalog.NoReferralRequired
                            : InspectionFollowUpStatusCatalog.CorrectiveFollowUpPending,
                    inspection.PhotoEvidence != null))
                .ToListAsync(cancellationToken);

            return Results.Ok(inspections);
        })
        .WithName("ListInspections")
        .WithSummary("Lists inspection records using supported metadata filters")
        .Produces<List<InspectionResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .AsNoTracking()
                .Include(candidate => candidate.WmsReferral)
                .Include(candidate => candidate.PhotoEvidence)
                .WhereOfficial()
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            return inspection is not null
                ? Results.Ok(InspectionResponse.FromInspection(inspection))
                : ApiErrors.NotFound("Inspection not found.");
        })
        .WithName("GetInspection")
        .WithSummary("Gets an inspection record by its identifier")
        .Produces<InspectionResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        return endpoints;
    }

}

public sealed record InspectionResponse(
    Guid Id,
    Guid ScheduleId,
    Guid AssetId,
    Guid InspectorUserId,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DateAccomplished = null,
    bool? WaterReplaceCarbonFilter = null,
    bool? WaterReplaceSedimentFilter = null,
    bool? WaterCheckUvLight = null,
    string? ExternalPmNumber = null,
    int WmsReferralRevision = 0,
    string CorrectiveFollowUpStatus = InspectionFollowUpStatusCatalog.NoReferralRequired,
    bool HasPhotoEvidence = false)
{
    internal static InspectionResponse FromInspection(InspectionRecord inspection)
    {
        return new InspectionResponse(
            inspection.Id,
            inspection.ScheduleId,
            inspection.AssetId,
            inspection.InspectorUserId,
            inspection.DateInspected,
            inspection.IsOperational,
            inspection.Remarks,
            inspection.ActionsRecommendations,
            inspection.CreatedAt,
            inspection.UpdatedAt,
            inspection.DateAccomplished,
            inspection.WaterReplaceCarbonFilter,
            inspection.WaterReplaceSedimentFilter,
            inspection.WaterCheckUvLight,
            inspection.WmsReferral?.ExternalPmNumber,
            inspection.WmsReferral?.Revision ?? 0,
            InspectionFollowUpStatusCatalog.For(
                inspection.IsOperational,
                inspection.WmsReferral is not null),
            inspection.PhotoEvidence is not null);
    }
}

public sealed class UpdateInspectionWmsReferralDto
{
    public string ExternalPmNumber { get; set; } = string.Empty;
    public int ExpectedRevision { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var normalized = ExternalPmNumber?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            errors[nameof(ExternalPmNumber)] = ["External WMS PM number is required."];
        }
        else if (normalized.Length > 128)
        {
            errors[nameof(ExternalPmNumber)] = ["External WMS PM number must be 128 characters or fewer."];
        }

        if (ExpectedRevision < 0)
        {
            errors[nameof(ExpectedRevision)] = ["Expected revision must not be negative."];
        }

        return errors;
    }
}

public sealed record InspectionWmsReferralResponse(
    Guid InspectionId,
    string ExternalPmNumber,
    int Revision,
    Guid RecordedByUserId,
    DateTimeOffset RecordedAt,
    Guid LastUpdatedByUserId,
    DateTimeOffset LastUpdatedAt,
    string FollowUpStatus)
{
    internal static InspectionWmsReferralResponse FromReferral(InspectionWmsReferral referral)
    {
        return new InspectionWmsReferralResponse(
            referral.InspectionId,
            referral.ExternalPmNumber,
            referral.Revision,
            referral.RecordedByUserId,
            referral.RecordedAt,
            referral.LastUpdatedByUserId,
            referral.LastUpdatedAt,
            InspectionFollowUpStatusCatalog.ReferredToWms);
    }
}

public sealed record InspectionWmsReferralDetailResponse(
    Guid InspectionId,
    string FollowUpStatus,
    string? ExternalPmNumber,
    int Revision,
    Guid? RecordedByUserId,
    DateTimeOffset? RecordedAt,
    Guid? LastUpdatedByUserId,
    DateTimeOffset? LastUpdatedAt,
    bool CanRecordReferral,
    string? EligibilityMessage,
    IReadOnlyList<InspectionWmsReferralAuditResponse> Audit)
{
    internal static InspectionWmsReferralDetailResponse FromInspection(
        InspectionRecord inspection,
        bool hasAcknowledgement,
        IReadOnlyList<InspectionWmsReferralAuditResponse> audit)
    {
        var referral = inspection.WmsReferral;
        var eligibilityMessage = EligibilityError(inspection, hasAcknowledgement);
        return new InspectionWmsReferralDetailResponse(
            inspection.Id,
            InspectionFollowUpStatusCatalog.For(inspection.IsOperational, referral is not null),
            referral?.ExternalPmNumber,
            referral?.Revision ?? 0,
            referral?.RecordedByUserId,
            referral?.RecordedAt,
            referral?.LastUpdatedByUserId,
            referral?.LastUpdatedAt,
            eligibilityMessage is null,
            eligibilityMessage,
            audit);
    }

    internal static string? EligibilityError(InspectionRecord inspection, bool hasAcknowledgement)
    {
        if (inspection.PreventiveMaintenanceFormId is null
            || !string.Equals(
                inspection.PreventiveMaintenanceForm?.Status,
                PreventiveMaintenanceFormStatusCatalog.Acknowledged,
                StringComparison.Ordinal)
            || !hasAcknowledgement)
        {
            return "An acknowledged preventive-maintenance form is required before recording a WMS PM number.";
        }

        if (inspection.CompletedAt is null)
        {
            return "Field work must be completed before recording a WMS PM number.";
        }

        if (inspection.IsOperational)
        {
            return "Only non-operational inspections can have a corrective WMS PM number.";
        }

        return null;
    }
}

public sealed record InspectionWmsReferralAuditResponse(
    string? PreviousExternalPmNumber,
    string NewExternalPmNumber,
    int Revision,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAt);

public sealed record InspectionHistoryResponse(
    Guid Id,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations,
    DateTimeOffset? DateAccomplished = null,
    bool? WaterReplaceCarbonFilter = null,
    bool? WaterReplaceSedimentFilter = null,
    bool? WaterCheckUvLight = null);
