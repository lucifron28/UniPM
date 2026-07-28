using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Features.Retrieval;
using UniPM.Api.Features.Schedules;
using UniPM.Api.Models;

namespace UniPM.Api.Features.PreventiveMaintenanceForms;

public static class PreventiveMaintenanceFormEndpoints
{
    public static IEndpointRouteBuilder MapPreventiveMaintenanceFormEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/preventive-maintenance-forms")
            .WithTags("Preventive Maintenance Forms");

        group.MapPost("/", async (
            CreatePreventiveMaintenanceFormDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!TryGetAuthenticatedUserId(principal, out var createdByUserId))
            {
                return ApiErrors.Unauthorized("The authenticated user is unavailable.");
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var form = new PreventiveMaintenanceForm
            {
                Id = Guid.NewGuid(),
                AssetCategory = NormalizeAssetCategory(dto.AssetCategory),
                Building = NormalizeOptional(dto.Building),
                Department = NormalizeOptional(dto.Department),
                PeriodType = NormalizePeriodType(dto.PeriodType),
                Quarter = NormalizeQuarter(dto.Quarter),
                Semester = NormalizeSemester(dto.Semester),
                Year = dto.Year,
                AcademicYear = NormalizeOptional(dto.AcademicYear),
                Status = PreventiveMaintenanceFormStatusCatalog.Draft,
                CreatedByUserId = createdByUserId,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.PreventiveMaintenanceForms.Add(form);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/v1/preventive-maintenance-forms/{form.Id}",
                PreventiveMaintenanceFormResponse.FromForm(form));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("CreatePreventiveMaintenanceFormDraft")
        .WithSummary("Creates a preventive-maintenance form draft")
        .Produces<PreventiveMaintenanceFormResponse>(StatusCodes.Status201Created)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var forms = await context.PreventiveMaintenanceForms
                .AsNoTracking()
                .Include(form => form.Inspections)
                .OrderByDescending(form => form.CreatedAt)
                .ThenBy(form => form.Id)
                .ToListAsync(cancellationToken);

            return Results.Ok(forms.Select(PreventiveMaintenanceFormResponse.FromForm).ToList());
        })
        .WithName("ListPreventiveMaintenanceForms")
        .WithSummary("Lists preventive-maintenance forms")
        .Produces<List<PreventiveMaintenanceFormResponse>>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .AsNoTracking()
                .Include(candidate => candidate.Inspections)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            return form is null
                ? ApiErrors.NotFound("Preventive-maintenance form not found.")
                : Results.Ok(PreventiveMaintenanceFormResponse.FromForm(form));
        })
        .WithName("GetPreventiveMaintenanceForm")
        .WithSummary("Gets a preventive-maintenance form")
        .Produces<PreventiveMaintenanceFormResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        group.MapPost("/{id}/submit", async (
            Guid id,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            PreventiveMaintenanceFileNumberGenerator fileNumberGenerator,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedUserId(principal, out var submittedByUserId))
            {
                return ApiErrors.Unauthorized("The authenticated user is unavailable.");
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                await using var context = await factory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await BeginSerializableTransactionIfRelationalAsync(context, cancellationToken);
                var form = await context.PreventiveMaintenanceForms
                    .Include(candidate => candidate.Inspections)
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (form is null)
                {
                    return ApiErrors.NotFound("Preventive-maintenance form not found.");
                }

                if (!IsDraft(form))
                {
                    return ApiErrors.Conflict("Only draft forms can be submitted.");
                }

                if (form.Inspections.Count == 0)
                {
                    return ApiErrors.Conflict("A preventive-maintenance form requires at least one inspection row before submission.");
                }

                if (principal.IsInRole(AuthRoleCatalog.Inspector)
                    && (form.CreatedByUserId != submittedByUserId
                        || form.Inspections.Any(inspection => inspection.InspectorUserId != submittedByUserId)))
                {
                    return Results.Forbid();
                }

                var now = DateTimeOffset.UtcNow;
                var seriesPrefix = fileNumberGenerator.CreateSeriesPrefix(now);
                var existingFileNumbers = await context.PreventiveMaintenanceForms
                    .AsNoTracking()
                    .Where(candidate => candidate.FileNumber != null
                        && candidate.FileNumber.StartsWith(seriesPrefix))
                    .Select(candidate => candidate.FileNumber!)
                    .ToListAsync(cancellationToken);

                try
                {
                    form.FileNumber = fileNumberGenerator.CreateNext(seriesPrefix, existingFileNumbers);
                }
                catch (PreventiveMaintenanceFileNumberSequenceExhaustedException)
                {
                    return ApiErrors.Conflict(
                        "The provisional file-number sequence is exhausted for the current year.");
                }

                form.SubmittedByUserId = submittedByUserId;
                form.SubmittedAt = now;
                form.Status = PreventiveMaintenanceFormStatusCatalog.Submitted;
                form.UpdatedAt = now;

                try
                {
                    await context.SaveChangesAsync(cancellationToken);
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    return Results.Ok(PreventiveMaintenanceFormResponse.FromForm(form));
                }
                catch (Exception exception)
                    when (DatabaseConstraintViolation.IsUniqueConstraint(exception)
                        || DatabaseConstraintViolation.IsDeadlock(exception))
                {
                    if (attempt == 2)
                    {
                        break;
                    }

                    // A concurrent submission claimed the candidate number. Retry with a new context.
                }
            }

            return ApiErrors.Conflict("A unique provisional file number could not be assigned. Please retry the submission.");
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("SubmitPreventiveMaintenanceForm")
        .WithSummary("Submits a completed preventive-maintenance form using a provisional file number")
        .Produces<PreventiveMaintenanceFormResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id}/acknowledge", async (
            Guid id,
            AcknowledgePreventiveMaintenanceFormDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            MaintenanceSearchDocumentProjector projector,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate(out var signatureBytes);
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!TryGetAuthenticatedUserId(principal, out var capturedByUserId))
            {
                return ApiErrors.Unauthorized("The authenticated user is unavailable.");
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await BeginTransactionIfRelationalAsync(context, cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .Include(candidate => candidate.Acknowledgement)
                .Include(candidate => candidate.Inspections)
                    .ThenInclude(inspection => inspection.Schedule)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (form is null)
            {
                return ApiErrors.NotFound("Preventive-maintenance form not found.");
            }

            if (form.Acknowledgement is not null)
            {
                return ApiErrors.Conflict("The preventive-maintenance form has already been acknowledged.");
            }

            if (!string.Equals(
                    form.Status,
                    PreventiveMaintenanceFormStatusCatalog.Submitted,
                    StringComparison.Ordinal))
            {
                return ApiErrors.Conflict("Only submitted forms can be acknowledged.");
            }

            if (principal.IsInRole(AuthRoleCatalog.Inspector)
                && (form.CreatedByUserId != capturedByUserId
                    || form.Inspections.Any(inspection => inspection.InspectorUserId != capturedByUserId)))
            {
                return Results.Forbid();
            }

            if (form.Inspections.Any(inspection => inspection.Schedule is null))
            {
                return ApiErrors.Conflict("Every form row must reference an available schedule before acknowledgement.");
            }

            var now = DateTimeOffset.UtcNow;
            var acknowledgement = new PreventiveMaintenanceAcknowledgement
            {
                Id = Guid.NewGuid(),
                FormId = form.Id,
                SignatoryName = dto.SignatoryName.Trim(),
                SignatoryPosition = dto.SignatoryPosition.Trim(),
                SignatureData = Convert.ToBase64String(signatureBytes),
                SignatureContentType = AcknowledgePreventiveMaintenanceFormDto.PngContentType,
                SignatureChecksum = Convert.ToHexString(SHA256.HashData(signatureBytes)),
                CapturedByUserId = capturedByUserId,
                AcknowledgedAt = now
            };

            context.PreventiveMaintenanceAcknowledgements.Add(acknowledgement);
            form.Status = PreventiveMaintenanceFormStatusCatalog.Acknowledged;
            form.UpdatedAt = now;
            foreach (var inspection in form.Inspections)
            {
                inspection.Schedule!.Status = ScheduleStatusCatalog.Completed;
                inspection.Schedule.CompletedAt = now;
                inspection.Schedule.UpdatedAt = now;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                await projector.RebuildAsync(
                    context,
                    form.Inspections.Select(inspection => inspection.Id).ToHashSet(),
                    cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return ApiErrors.Conflict("The preventive-maintenance form acknowledgement changed concurrently.");
            }
            catch (Exception exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
            {
                return ApiErrors.Conflict("The preventive-maintenance form has already been acknowledged.");
            }

            return Results.Ok(PreventiveMaintenanceAcknowledgementResponse.FromAcknowledgement(acknowledgement));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("AcknowledgePreventiveMaintenanceForm")
        .WithSummary("Acknowledges one submitted preventive-maintenance form")
        .Produces<PreventiveMaintenanceAcknowledgementResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("/{id}/corrective-handoff", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (form is null)
            {
                return ApiErrors.NotFound("Preventive-maintenance form not found.");
            }

            if (!string.Equals(
                    form.Status,
                    PreventiveMaintenanceFormStatusCatalog.Acknowledged,
                    StringComparison.Ordinal))
            {
                return ApiErrors.Conflict("Only acknowledged forms can produce a corrective-maintenance handoff.");
            }

            var acknowledgedAt = await context.PreventiveMaintenanceAcknowledgements
                .AsNoTracking()
                .Where(acknowledgement => acknowledgement.FormId == form.Id)
                .Select(acknowledgement => (DateTimeOffset?)acknowledgement.AcknowledgedAt)
                .SingleOrDefaultAsync(cancellationToken);
            if (acknowledgedAt is null)
            {
                return ApiErrors.Conflict("Acknowledgement metadata is unavailable for this form.");
            }

            var sourceRows = await context.InspectionRecords
                .AsNoTracking()
                .Where(inspection => inspection.PreventiveMaintenanceFormId == form.Id
                    && inspection.ActionsRecommendations != null
                    && inspection.ActionsRecommendations != "")
                .OrderBy(inspection => inspection.DateInspected)
                .ThenBy(inspection => inspection.Id)
                .Select(inspection => new CorrectiveMaintenanceHandoffSourceRow(
                    inspection.Id,
                    inspection.DateInspected,
                    null,
                    inspection.Asset.AssetCode,
                    inspection.Asset.Location,
                    inspection.Remarks,
                    inspection.IsOperational,
                    inspection.ActionsRecommendations!,
                    inspection.InspectorUserId))
                .ToListAsync(cancellationToken);

            var inspectorIds = sourceRows
                .Select(row => row.InspectorUserId)
                .Distinct()
                .ToArray();
            var users = await context.Users
                .AsNoTracking()
                .Where(user => inspectorIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
                    cancellationToken);

            var rows = sourceRows
                .Select(row => new CorrectiveMaintenanceHandoffRowResponse(
                    row.InspectionId,
                    row.InspectionDate,
                    row.AssetDeviceNumber,
                    row.AssetCode,
                    row.Location,
                    row.FindingOrRemarks,
                    row.IsOperational,
                    row.RecommendedCorrectiveAction,
                    row.InspectorUserId,
                    users.GetValueOrDefault(row.InspectorUserId)))
                .ToList();

            return Results.Ok(new CorrectiveMaintenanceHandoffResponse(
                form.Id,
                form.FileNumber,
                acknowledgedAt.Value,
                form.Department,
                form.Building,
                form.AssetCategory,
                rows.Count > 0,
                rows));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanAccessCorrectiveMaintenanceHandoff)
        .WithName("GetCorrectiveMaintenanceHandoff")
        .WithSummary("Prepares acknowledged preventive-maintenance findings for corrective follow-up")
        .Produces<CorrectiveMaintenanceHandoffResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{id}/inspections", async (
            Guid id,
            DraftInspectionRowDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!CanUseInspectorUserId(principal, dto.InspectorUserId))
            {
                return Results.Forbid();
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (form is null)
            {
                return ApiErrors.NotFound("Preventive-maintenance form not found.");
            }

            if (!IsDraft(form))
            {
                return ApiErrors.Conflict("Only draft forms can be edited.");
            }

            var schedule = await context.PreventiveMaintenanceSchedules
                .Include(candidate => candidate.Asset)
                .SingleOrDefaultAsync(candidate => candidate.Id == dto.ScheduleId, cancellationToken);
            if (schedule is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            if (schedule.Asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            if (!string.Equals(schedule.Asset.AssetCategory, form.AssetCategory, StringComparison.Ordinal))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.ScheduleId)] = ["Schedule asset category must match the form asset category."]
                });
            }

            if (await context.InspectionRecords.AnyAsync(
                    inspection => inspection.ScheduleId == dto.ScheduleId,
                    cancellationToken))
            {
                return ApiErrors.Conflict("Schedule already has a recorded inspection.");
            }

            var inspector = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == dto.InspectorUserId, cancellationToken);
            if (inspector is null || !inspector.IsActive)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.InspectorUserId)] = ["Inspector user is unavailable."]
                });
            }

            var now = DateTimeOffset.UtcNow;
            var inspection = CreateInspection(dto, schedule.AssetId, form.Id, now);
            context.InspectionRecords.Add(inspection);
            form.UpdatedAt = now;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
            {
                return ApiErrors.Conflict("Schedule already has a recorded inspection.");
            }

            return Results.Created(
                $"/api/v1/preventive-maintenance-forms/{form.Id}/inspections/{inspection.Id}",
                DraftInspectionRowResponse.FromInspection(inspection));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("AddPreventiveMaintenanceFormDraftInspection")
        .WithSummary("Adds an inspection row to a preventive-maintenance form draft")
        .Produces<DraftInspectionRowResponse>(StatusCodes.Status201Created)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{id}/inspections/{inspectionId}", async (
            Guid id,
            Guid inspectionId,
            UpdateDraftInspectionRowDto dto,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
            }

            if (!CanUseInspectorUserId(principal, dto.InspectorUserId))
            {
                return Results.Forbid();
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (form is null)
            {
                return ApiErrors.NotFound("Preventive-maintenance form not found.");
            }

            if (!IsDraft(form))
            {
                return ApiErrors.Conflict("Only draft forms can be edited.");
            }

            var inspection = await context.InspectionRecords
                .SingleOrDefaultAsync(candidate => candidate.Id == inspectionId
                    && candidate.PreventiveMaintenanceFormId == form.Id,
                    cancellationToken);
            if (inspection is null)
            {
                return ApiErrors.NotFound("Draft inspection row not found.");
            }

            if (!CanUseInspectorUserId(principal, inspection.InspectorUserId))
            {
                return Results.Forbid();
            }

            var inspector = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == dto.InspectorUserId, cancellationToken);
            if (inspector is null || !inspector.IsActive)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(dto.InspectorUserId)] = ["Inspector user is unavailable."]
                });
            }

            inspection.InspectorUserId = dto.InspectorUserId;
            inspection.DateInspected = dto.DateInspected;
            inspection.IsOperational = dto.IsOperational;
            inspection.Remarks = NormalizeOptional(dto.Remarks);
            inspection.ActionsRecommendations = NormalizeOptional(dto.ActionsRecommendations);
            inspection.UpdatedAt = DateTimeOffset.UtcNow;
            form.UpdatedAt = inspection.UpdatedAt;
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(DraftInspectionRowResponse.FromInspection(inspection));
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("UpdatePreventiveMaintenanceFormDraftInspection")
        .WithSummary("Updates an inspection row in a preventive-maintenance form draft")
        .Produces<DraftInspectionRowResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapDelete("/{id}/inspections/{inspectionId}", async (
            Guid id,
            Guid inspectionId,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var form = await context.PreventiveMaintenanceForms
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (form is null)
            {
                return ApiErrors.NotFound("Preventive-maintenance form not found.");
            }

            if (!IsDraft(form))
            {
                return ApiErrors.Conflict("Only draft forms can be edited.");
            }

            var inspection = await context.InspectionRecords
                .SingleOrDefaultAsync(candidate => candidate.Id == inspectionId
                    && candidate.PreventiveMaintenanceFormId == form.Id,
                    cancellationToken);
            if (inspection is null)
            {
                return ApiErrors.NotFound("Draft inspection row not found.");
            }

            if (!CanUseInspectorUserId(principal, inspection.InspectorUserId))
            {
                return Results.Forbid();
            }

            context.InspectionRecords.Remove(inspection);
            form.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("DeletePreventiveMaintenanceFormDraftInspection")
        .WithSummary("Removes an inspection row from a preventive-maintenance form draft")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static InspectionRecord CreateInspection(
        DraftInspectionRowDto dto,
        Guid assetId,
        Guid formId,
        DateTimeOffset now)
    {
        return new InspectionRecord
        {
            Id = Guid.NewGuid(),
            ScheduleId = dto.ScheduleId,
            PreventiveMaintenanceFormId = formId,
            AssetId = assetId,
            InspectorUserId = dto.InspectorUserId,
            DateInspected = dto.DateInspected,
            IsOperational = dto.IsOperational,
            Remarks = NormalizeOptional(dto.Remarks),
            ActionsRecommendations = NormalizeOptional(dto.ActionsRecommendations),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static bool IsDraft(PreventiveMaintenanceForm form)
    {
        return string.Equals(form.Status, PreventiveMaintenanceFormStatusCatalog.Draft, StringComparison.Ordinal);
    }

    private static bool TryGetAuthenticatedUserId(ClaimsPrincipal principal, out Guid userId)
    {
        return Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out userId);
    }

    private static bool CanUseInspectorUserId(ClaimsPrincipal principal, Guid inspectorUserId)
    {
        return !principal.IsInRole(AuthRoleCatalog.Inspector)
            || TryGetAuthenticatedUserId(principal, out var authenticatedUserId)
            && authenticatedUserId == inspectorUserId;
    }

    private static string NormalizeAssetCategory(string value)
    {
        _ = AssetCategoryCatalog.TryNormalize(value, out var normalized);
        return normalized;
    }

    private static string NormalizePeriodType(string value)
    {
        _ = SchedulePeriodTypeCatalog.TryNormalize(value, out var normalized);
        return normalized;
    }

    private static string? NormalizeQuarter(string? value)
    {
        _ = ScheduleQuarterCatalog.TryNormalizeNullable(value, out var normalized);
        return normalized;
    }

    private static string? NormalizeSemester(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();
        return ScheduleSemesterCatalog.PersistedValues.First(allowed =>
            string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task<IDbContextTransaction?> BeginSerializableTransactionIfRelationalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
    }

    private static async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }
}

public sealed class AcknowledgePreventiveMaintenanceFormDto
{
    internal const string PngContentType = "image/png";
    private const int SignatoryMaxLength = 160;
    private const int SignatureDataMaxLength = 262_144;
    private const int SignatureContentTypeMaxLength = 128;
    private const int DecodedSignatureMaxBytes = 196_608;
    private static readonly byte[] PngHeader = [137, 80, 78, 71, 13, 10, 26, 10];

    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryPosition { get; set; } = string.Empty;
    public string SignatureData { get; set; } = string.Empty;
    public string SignatureContentType { get; set; } = string.Empty;

    internal Dictionary<string, string[]> Validate(out byte[] signatureBytes)
    {
        var errors = new Dictionary<string, string[]>();
        signatureBytes = [];
        AddRequiredLengthError(SignatoryName, nameof(SignatoryName), "Signatory name", errors);
        AddRequiredLengthError(SignatoryPosition, nameof(SignatoryPosition), "Signatory position", errors);

        var normalizedContentType = SignatureContentType?.Trim() ?? string.Empty;
        if (normalizedContentType.Length == 0)
        {
            errors.Add(nameof(SignatureContentType), ["Signature content type is required."]);
        }
        else if (normalizedContentType.Length > SignatureContentTypeMaxLength)
        {
            errors.Add(nameof(SignatureContentType),
                [$"Signature content type must not exceed {SignatureContentTypeMaxLength} characters."]);
        }
        else if (!string.Equals(normalizedContentType, PngContentType, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(nameof(SignatureContentType), ["Signature content type must be image/png."]);
        }

        var normalizedSignature = SignatureData?.Trim() ?? string.Empty;
        if (normalizedSignature.Length == 0)
        {
            errors.Add(nameof(SignatureData), ["Signature data is required."]);
            return errors;
        }

        if (normalizedSignature.Length > SignatureDataMaxLength)
        {
            errors.Add(nameof(SignatureData),
                [$"Signature data must not exceed {SignatureDataMaxLength} base64 characters."]);
            return errors;
        }

        try
        {
            signatureBytes = Convert.FromBase64String(normalizedSignature);
        }
        catch (FormatException)
        {
            errors.Add(nameof(SignatureData), ["Signature data must be valid base64."]);
            return errors;
        }

        if (signatureBytes.Length > DecodedSignatureMaxBytes)
        {
            errors.Add(nameof(SignatureData),
                [$"Decoded signature data must not exceed {DecodedSignatureMaxBytes} bytes."]);
        }
        else if (!signatureBytes.AsSpan().StartsWith(PngHeader))
        {
            errors.Add(nameof(SignatureData), ["Signature data must contain a PNG image."]);
        }

        return errors;
    }

    private static void AddRequiredLengthError(
        string? value,
        string propertyName,
        string label,
        Dictionary<string, string[]> errors)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            errors.Add(propertyName, [$"{label} is required."]);
        }
        else if (normalized.Length > SignatoryMaxLength)
        {
            errors.Add(propertyName, [$"{label} must not exceed {SignatoryMaxLength} characters."]);
        }
    }
}

public sealed record PreventiveMaintenanceAcknowledgementResponse(
    Guid Id,
    Guid FormId,
    string SignatoryName,
    string SignatoryPosition,
    string SignatureContentType,
    string SignatureChecksum,
    Guid CapturedByUserId,
    DateTimeOffset AcknowledgedAt)
{
    internal static PreventiveMaintenanceAcknowledgementResponse FromAcknowledgement(
        PreventiveMaintenanceAcknowledgement acknowledgement)
    {
        return new PreventiveMaintenanceAcknowledgementResponse(
            acknowledgement.Id,
            acknowledgement.FormId,
            acknowledgement.SignatoryName,
            acknowledgement.SignatoryPosition,
            acknowledgement.SignatureContentType!,
            acknowledgement.SignatureChecksum!,
            acknowledgement.CapturedByUserId,
            acknowledgement.AcknowledgedAt);
    }
}

public sealed class CreatePreventiveMaintenanceFormDto
{
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string? Quarter { get; set; }
    public string? Semester { get; set; }
    public int? Year { get; set; }
    public string? AcademicYear { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var hasSupportedCategory = AssetCategoryCatalog.TryNormalize(AssetCategory, out _);
        if (string.IsNullOrWhiteSpace(AssetCategory))
        {
            errors.Add(nameof(AssetCategory), ["Asset category is required."]);
        }
        else if (!hasSupportedCategory)
        {
            errors.Add(nameof(AssetCategory), ["Asset category must be supported."]);
        }

        var hasSupportedPeriodType = SchedulePeriodTypeCatalog.TryNormalize(PeriodType, out var periodType);
        if (string.IsNullOrWhiteSpace(PeriodType))
        {
            errors.Add(nameof(PeriodType), ["Period type is required."]);
        }
        else if (!hasSupportedPeriodType)
        {
            errors.Add(nameof(PeriodType), ["Period type must be supported."]);
        }

        if (hasSupportedPeriodType
            && string.Equals(periodType, SchedulePeriodTypeCatalog.Quarter, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(Quarter))
        {
            errors.Add(nameof(Quarter), ["Quarter is required for quarterly forms."]);
        }

        if (!ScheduleQuarterCatalog.TryNormalizeNullable(Quarter, out _))
        {
            errors.Add(nameof(Quarter), ["Quarter must be one of Q1, Q2, Q3, or Q4."]);
        }

        if (!string.IsNullOrWhiteSpace(Semester)
            && !ScheduleSemesterCatalog.PersistedValues.Any(value =>
                string.Equals(value, Semester.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(nameof(Semester), ["Semester must be a supported semester."]);
        }

        AddOptionalLengthError(Building, nameof(Building), "Building", errors);
        AddOptionalLengthError(Department, nameof(Department), "Department", errors);

        if (!string.IsNullOrWhiteSpace(PeriodType) && PeriodType.Trim().Length > 32)
        {
            errors.Add(nameof(PeriodType), ["Period type must not exceed 32 characters."]);
        }

        if (Year is not null)
        {
            var maximumYear = DateTimeOffset.UtcNow.Year + 5;
            if (Year < 2000 || Year > maximumYear)
            {
                errors.Add(nameof(Year), [$"Year must be between 2000 and {maximumYear}."]);
            }
        }

        if (!string.IsNullOrWhiteSpace(AcademicYear)
            && !PreventiveMaintenanceFormEndpointsAcademicYear.IsValid(AcademicYear.Trim()))
        {
            errors.Add(nameof(AcademicYear), ["Academic year must use YYYY-YYYY format."]);
        }

        return errors;
    }

    private static void AddOptionalLengthError(
        string? value,
        string propertyName,
        string label,
        Dictionary<string, string[]> errors)
    {
        if (value is { Length: > 256 })
        {
            errors.Add(propertyName, [$"{label} must not exceed 256 characters."]);
        }
    }
}

public sealed class DraftInspectionRowDto
{
    public Guid ScheduleId { get; set; }
    public Guid InspectorUserId { get; set; }
    public DateTimeOffset DateInspected { get; set; }
    public bool IsOperational { get; set; }
    public string? Remarks { get; set; }
    public string? ActionsRecommendations { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = ValidateInspectionDetails(
            InspectorUserId,
            DateInspected,
            Remarks,
            ActionsRecommendations);
        if (ScheduleId == Guid.Empty)
        {
            errors.Add(nameof(ScheduleId), ["Schedule ID is required."]);
        }

        return errors;
    }

    internal static Dictionary<string, string[]> ValidateInspectionDetails(
        Guid inspectorUserId,
        DateTimeOffset dateInspected,
        string? remarks,
        string? actionsRecommendations)
    {
        var errors = new Dictionary<string, string[]>();
        if (inspectorUserId == Guid.Empty)
        {
            errors.Add(nameof(InspectorUserId), ["Inspector user ID is required."]);
        }

        if (dateInspected == default)
        {
            errors.Add(nameof(DateInspected), ["Date inspected is required."]);
        }
        else if (dateInspected > DateTimeOffset.UtcNow.AddDays(1))
        {
            errors.Add(nameof(DateInspected), ["Date inspected cannot be more than one day in the future."]);
        }

        if (remarks?.Length > 2_000)
        {
            errors.Add(nameof(Remarks), ["Remarks must be 2,000 characters or fewer."]);
        }

        if (actionsRecommendations?.Length > 2_000)
        {
            errors.Add(nameof(ActionsRecommendations), ["Actions and recommendations must be 2,000 characters or fewer."]);
        }

        return errors;
    }
}

public sealed class UpdateDraftInspectionRowDto
{
    public Guid InspectorUserId { get; set; }
    public DateTimeOffset DateInspected { get; set; }
    public bool IsOperational { get; set; }
    public string? Remarks { get; set; }
    public string? ActionsRecommendations { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        return DraftInspectionRowDto.ValidateInspectionDetails(
            InspectorUserId,
            DateInspected,
            Remarks,
            ActionsRecommendations);
    }
}

public sealed record PreventiveMaintenanceFormResponse(
    Guid Id,
    string? FileNumber,
    string AssetCategory,
    string? Building,
    string? Department,
    string PeriodType,
    string? Quarter,
    string? Semester,
    int? Year,
    string? AcademicYear,
    string Status,
    Guid CreatedByUserId,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DraftInspectionRowResponse> Inspections)
{
    internal static PreventiveMaintenanceFormResponse FromForm(PreventiveMaintenanceForm form)
    {
        return new PreventiveMaintenanceFormResponse(
            form.Id,
            form.FileNumber,
            form.AssetCategory,
            form.Building,
            form.Department,
            form.PeriodType,
            form.Quarter,
            form.Semester,
            form.Year,
            form.AcademicYear,
            form.Status,
            form.CreatedByUserId,
            form.SubmittedByUserId,
            form.SubmittedAt,
            form.CreatedAt,
            form.UpdatedAt,
            form.Inspections
                .OrderBy(inspection => inspection.DateInspected)
                .ThenBy(inspection => inspection.Id)
                .Select(DraftInspectionRowResponse.FromInspection)
                .ToList());
    }
}

public sealed record DraftInspectionRowResponse(
    Guid Id,
    Guid ScheduleId,
    Guid AssetId,
    Guid InspectorUserId,
    DateTimeOffset DateInspected,
    bool IsOperational,
    string? Remarks,
    string? ActionsRecommendations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    internal static DraftInspectionRowResponse FromInspection(InspectionRecord inspection)
    {
        return new DraftInspectionRowResponse(
            inspection.Id,
            inspection.ScheduleId,
            inspection.AssetId,
            inspection.InspectorUserId,
            inspection.DateInspected,
            inspection.IsOperational,
            inspection.Remarks,
            inspection.ActionsRecommendations,
            inspection.CreatedAt,
            inspection.UpdatedAt);
    }
}

public sealed record CorrectiveMaintenanceHandoffResponse(
    Guid FormId,
    string? FileNumber,
    DateTimeOffset AcknowledgedAt,
    string? Department,
    string? Building,
    string AssetCategory,
    bool HasCorrectiveActionRows,
    IReadOnlyList<CorrectiveMaintenanceHandoffRowResponse> Rows);

public sealed record CorrectiveMaintenanceHandoffRowResponse(
    Guid InspectionId,
    DateTimeOffset InspectionDate,
    string? AssetDeviceNumber,
    string AssetCode,
    string? Location,
    string? FindingOrRemarks,
    bool IsOperational,
    string RecommendedCorrectiveAction,
    Guid SkilledWorkerUserId,
    string? SkilledWorkerIdentity);

internal sealed record CorrectiveMaintenanceHandoffSourceRow(
    Guid InspectionId,
    DateTimeOffset InspectionDate,
    string? AssetDeviceNumber,
    string AssetCode,
    string? Location,
    string? FindingOrRemarks,
    bool IsOperational,
    string RecommendedCorrectiveAction,
    Guid InspectorUserId);

internal static class PreventiveMaintenanceFormEndpointsAcademicYear
{
    private static readonly Regex Pattern = new("^[0-9]{4}-[0-9]{4}$", RegexOptions.CultureInvariant);

    internal static bool IsValid(string value) => Pattern.IsMatch(value);
}
