using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Assets;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionLocationVerificationEndpoints
{
    public static IEndpointRouteBuilder MapInspectionLocationVerificationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/schedules/{scheduleId:guid}/location-verification-attempts", async (
            Guid scheduleId,
            CreateInspectionLocationAttemptDto dto,
            ClaimsPrincipal principal,
            IAuthorizationService authorizationService,
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
            var schedule = await context.PreventiveMaintenanceSchedules
                .Include(candidate => candidate.Asset)
                .SingleOrDefaultAsync(candidate => candidate.Id == scheduleId, cancellationToken);
            if (schedule is null)
            {
                return ApiErrors.NotFound("Schedule not found.");
            }

            var scheduleAccess = await authorizationService.AuthorizeAsync(
                principal,
                schedule,
                AuthPolicyCatalog.CanInspectPreventiveMaintenanceSchedule);
            if (!scheduleAccess.Succeeded)
            {
                return Results.Forbid();
            }

            var hasDraftFormRow = PreventiveMaintenanceFormBatchPolicy.IsCompletedScheduleStatus(schedule.Status)
                && await context.InspectionRecords.AnyAsync(
                    inspection => inspection.ScheduleId == schedule.Id
                        && inspection.PreventiveMaintenanceForm != null
                        && inspection.PreventiveMaintenanceForm.Status == PreventiveMaintenanceFormStatusCatalog.Draft,
                    cancellationToken);
            if (!PreventiveMaintenanceFormBatchPolicy.IsEligibleScheduleStatus(schedule.Status)
                && !hasDraftFormRow)
            {
                return ApiErrors.Conflict(
                    "Location verification is available only for an eligible schedule or its existing draft form row.");
            }

            if (schedule.Asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            var classification = InspectionLocationClassifier.Classify(
                schedule.Asset.VerificationLatitude,
                schedule.Asset.VerificationLongitude,
                schedule.Asset.VerificationRadiusMeters,
                dto.Latitude,
                dto.Longitude,
                dto.HasAccuracy,
                dto.AccuracyMeters);
            var capturedAt = DateTimeOffset.UtcNow;
            var attempt = new InspectionLocationAttempt
            {
                Id = Guid.NewGuid(),
                AssetId = schedule.AssetId,
                ScheduleId = schedule.Id,
                ActorUserId = actorUserId,
                CapturedAt = capturedAt,
                DevicePositionTimestamp = dto.DevicePositionTimestamp,
                MeasuredLatitude = dto.Latitude,
                MeasuredLongitude = dto.Longitude,
                AccuracyMeters = dto.AccuracyMeters,
                HasAccuracy = dto.HasAccuracy,
                IsMocked = dto.IsMocked,
                AccuracyMode = dto.AccuracyMode,
                AcquisitionDurationMs = dto.AcquisitionDurationMs,
                ExpectedLatitude = schedule.Asset.VerificationLatitude,
                ExpectedLongitude = schedule.Asset.VerificationLongitude,
                ExpectedRadiusMeters = schedule.Asset.VerificationRadiusMeters,
                DistanceMeters = classification.DistanceMeters,
                Outcome = classification.Outcome
            };

            context.InspectionLocationAttempts.Add(attempt);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(InspectionLocationAttemptResponse.FromAttempt(attempt));
        })
        .WithTags("Schedules")
        .WithName("CreateInspectionLocationAttempt")
        .WithSummary("Records an inspection location verification attempt")
        .Produces<InspectionLocationAttemptResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms);

        return endpoints;
    }
}

public sealed class CreateInspectionLocationAttemptDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public required bool HasAccuracy { get; set; }
    public DateTimeOffset? DevicePositionTimestamp { get; set; }
    public required bool IsMocked { get; set; }
    public required string AccuracyMode { get; set; }
    public required int AcquisitionDurationMs { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (!AssetVerificationLocationRules.IsValidLatitude(Latitude))
        {
            errors[nameof(Latitude)] = ["Latitude must be between -90 and 90."];
        }

        if (!AssetVerificationLocationRules.IsValidLongitude(Longitude))
        {
            errors[nameof(Longitude)] = ["Longitude must be between -180 and 180."];
        }

        if (HasAccuracy && (AccuracyMeters is not { } accuracyValue
            || !double.IsFinite(accuracyValue)
            || accuracyValue < 0))
        {
            errors[nameof(AccuracyMeters)] = ["A valid accuracy value is required when accuracy is available."];
        }
        else if (!HasAccuracy && AccuracyMeters is not null)
        {
            errors[nameof(AccuracyMeters)] = ["Accuracy must be omitted when it is unavailable."];
        }

        if (AccuracyMode is not ("Precise" or "Reduced" or "Unknown"))
        {
            errors[nameof(AccuracyMode)] = ["Accuracy mode must be Precise, Reduced, or Unknown."];
        }

        if (AcquisitionDurationMs < 0)
        {
            errors[nameof(AcquisitionDurationMs)] = ["Acquisition duration must be zero or greater."];
        }

        return errors;
    }
}

public sealed record InspectionLocationAttemptResponse(
    Guid Id,
    DateTimeOffset CapturedAt,
    double? AccuracyMeters,
    bool HasAccuracy,
    DateTimeOffset? DevicePositionTimestamp,
    bool IsMocked,
    string AccuracyMode,
    int AcquisitionDurationMs,
    double? DistanceMeters,
    string Outcome)
{
    internal static InspectionLocationAttemptResponse FromAttempt(InspectionLocationAttempt attempt)
        => new(
            attempt.Id,
            attempt.CapturedAt,
            attempt.AccuracyMeters,
            attempt.HasAccuracy,
            attempt.DevicePositionTimestamp,
            attempt.IsMocked,
            attempt.AccuracyMode,
            attempt.AcquisitionDurationMs,
            attempt.DistanceMeters,
            attempt.Outcome);
}
