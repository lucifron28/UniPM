using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.ReferenceData;
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
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules)
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
        .Produces<List<PreventiveMaintenanceFormResponse>>(StatusCodes.Status200OK);

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
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/inspections", async (
            Guid id,
            DraftInspectionRowDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
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
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules)
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
            DraftInspectionRowDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var errors = dto.Validate();
            if (errors.Count > 0)
            {
                return ApiErrors.Validation(errors);
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
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules)
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

            context.InspectionRecords.Remove(inspection);
            form.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManageSchedules)
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
        var errors = new Dictionary<string, string[]>();
        if (ScheduleId == Guid.Empty)
        {
            errors.Add(nameof(ScheduleId), ["Schedule ID is required."]);
        }

        if (InspectorUserId == Guid.Empty)
        {
            errors.Add(nameof(InspectorUserId), ["Inspector user ID is required."]);
        }

        if (DateInspected == default)
        {
            errors.Add(nameof(DateInspected), ["Date inspected is required."]);
        }
        else if (DateInspected > DateTimeOffset.UtcNow.AddDays(1))
        {
            errors.Add(nameof(DateInspected), ["Date inspected cannot be more than one day in the future."]);
        }

        if (Remarks?.Length > 2_000)
        {
            errors.Add(nameof(Remarks), ["Remarks must be 2,000 characters or fewer."]);
        }

        if (ActionsRecommendations?.Length > 2_000)
        {
            errors.Add(nameof(ActionsRecommendations), ["Actions and recommendations must be 2,000 characters or fewer."]);
        }

        return errors;
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

internal static class PreventiveMaintenanceFormEndpointsAcademicYear
{
    private static readonly Regex Pattern = new("^[0-9]{4}-[0-9]{4}$", RegexOptions.CultureInvariant);

    internal static bool IsValid(string value) => Pattern.IsMatch(value);
}
