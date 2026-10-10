using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.PreventiveMaintenanceForms;
using UniPM.Api.Models;

namespace UniPM.Api.Features.Inspections;

public static class InspectionPhotoEvidenceEndpoints
{
    public static IEndpointRouteBuilder MapInspectionPhotoEvidenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/inspections");

        group.MapPut("/{id:guid}/photo", async (
            Guid id,
            HttpRequest request,
            HttpContext httpContext,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            IInspectionPhotoEvidenceStorage storage,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole(AuthRoleCatalog.Inspector)
                || !TryGetActorId(principal, out var actorId))
            {
                return Results.Forbid();
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .Include(candidate => candidate.PreventiveMaintenanceForm)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (inspection is null)
            {
                return ApiErrors.NotFound("Inspection not found.");
            }

            if (inspection.InspectorUserId != actorId)
            {
                return Results.Forbid();
            }

            if (inspection.PreventiveMaintenanceForm is not { Status: PreventiveMaintenanceFormStatusCatalog.Draft })
            {
                return ApiErrors.Conflict("Photo evidence can only be changed while the form is a Draft.");
            }

            if (!string.Equals(
                request.ContentType?.Split(';', 2)[0].Trim(),
                "image/jpeg",
                StringComparison.OrdinalIgnoreCase))
            {
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            var maximumBytes = InspectionPhotoEvidenceOptions.MaximumUploadBytes;
            var requestSizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (requestSizeFeature is { IsReadOnly: false })
            {
                requestSizeFeature.MaxRequestBodySize = maximumBytes;
            }

            if (request.ContentLength > maximumBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var input = new byte[maximumBytes + 1];
            var inputLength = 0;
            while (inputLength < input.Length)
            {
                var read = await request.Body.ReadAsync(
                    input.AsMemory(inputLength),
                    cancellationToken);
                if (read == 0) break;
                inputLength += read;
            }

            if (inputLength > maximumBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            byte[] photo;
            try
            {
                photo = InspectionPhotoJpeg.StripMetadataAndValidate(input.AsSpan(0, inputLength));
            }
            catch (InvalidDataException exception)
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    ["photo"] = [exception.Message]
                });
            }

            if (photo.Length == 0 || photo.Length > maximumBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var storageKey = await storage.SaveAsync(photo, cancellationToken);
            var existing = await context.InspectionPhotoEvidence
                .SingleOrDefaultAsync(candidate => candidate.InspectionId == id, cancellationToken);
            var previousStorageKey = existing?.StorageKey;
            var now = DateTimeOffset.UtcNow;

            if (existing is null)
            {
                context.InspectionPhotoEvidence.Add(new InspectionPhotoEvidence
                {
                    InspectionId = inspection.Id,
                    StorageKey = storageKey,
                    LengthBytes = photo.Length,
                    UploadedAt = now,
                    Revision = 1
                });
            }
            else
            {
                existing.StorageKey = storageKey;
                existing.LengthBytes = photo.Length;
                existing.UploadedAt = now;
                existing.Revision++;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
                return ApiErrors.Conflict("Photo evidence changed. Refresh the inspection and retry.");
            }
            catch (DbUpdateException)
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
                throw;
            }

            if (previousStorageKey is not null)
            {
                try
                {
                    await storage.DeleteAsync(previousStorageKey, CancellationToken.None);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    loggerFactory.CreateLogger(nameof(InspectionPhotoEvidenceEndpoints))
                        .LogWarning(exception, "Could not remove a replaced inspection photo file.");
                }
            }

            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .Accepts<byte[]>("image/jpeg")
        .WithName("ReplaceInspectionPhotoEvidence")
        .WithSummary("Uploads or replaces one private JPEG photo for a draft inspection row")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status413PayloadTooLarge)
        .Produces(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/{id:guid}/photo", async (
            Guid id,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            IDbContextFactory<ApplicationDbContext> factory,
            IInspectionPhotoEvidenceStorage storage,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetActorId(principal, out var actorId))
            {
                return Results.Forbid();
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .AsNoTracking()
                .Include(candidate => candidate.PreventiveMaintenanceForm)
                .Include(candidate => candidate.PhotoEvidence)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (inspection is null || !CanReadPhoto(principal, actorId, inspection))
            {
                return ApiErrors.NotFound("Photo evidence not found.");
            }

            if (inspection.PhotoEvidence is not { } photoEvidence)
            {
                return ApiErrors.NotFound("No photo evidence is recorded for this inspection.");
            }

            var contents = await storage.ReadAsync(photoEvidence.StorageKey, cancellationToken);
            if (contents is null)
            {
                return ApiErrors.NotFound("Photo evidence not found.");
            }

            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.File(contents, "image/jpeg");
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("GetInspectionPhotoEvidence")
        .WithSummary("Gets authorized private JPEG photo evidence for an inspection row")
        .Produces<byte[]>(StatusCodes.Status200OK, contentType: "image/jpeg")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/photo", async (
            Guid id,
            ClaimsPrincipal principal,
            IDbContextFactory<ApplicationDbContext> factory,
            IInspectionPhotoEvidenceStorage storage,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            if (!principal.IsInRole(AuthRoleCatalog.Inspector)
                || !TryGetActorId(principal, out var actorId))
            {
                return Results.Forbid();
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var inspection = await context.InspectionRecords
                .Include(candidate => candidate.PreventiveMaintenanceForm)
                .Include(candidate => candidate.PhotoEvidence)
                .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (inspection is null || inspection.InspectorUserId != actorId)
            {
                return ApiErrors.NotFound("Inspection not found.");
            }

            if (inspection.PreventiveMaintenanceForm is not { Status: PreventiveMaintenanceFormStatusCatalog.Draft })
            {
                return ApiErrors.Conflict("Photo evidence can only be changed while the form is a Draft.");
            }

            if (inspection.PhotoEvidence is not { } photoEvidence)
            {
                return Results.NoContent();
            }

            var storageKey = photoEvidence.StorageKey;
            context.InspectionPhotoEvidence.Remove(photoEvidence);
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                loggerFactory.CreateLogger(nameof(InspectionPhotoEvidenceEndpoints))
                    .LogWarning(exception, "Could not remove deleted inspection photo evidence file.");
            }

            return Results.NoContent();
        })
        .RequireAuthorization(AuthPolicyCatalog.CanManagePreventiveMaintenanceForms)
        .WithName("DeleteInspectionPhotoEvidence")
        .WithSummary("Removes optional photo evidence from a draft inspection row")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static bool CanReadPhoto(
        ClaimsPrincipal principal,
        Guid actorId,
        InspectionRecord inspection)
    {
        if (principal.IsInRole(AuthRoleCatalog.Inspector))
        {
            return inspection.InspectorUserId == actorId;
        }

        return principal.IsInRole(AuthRoleCatalog.Gsd)
            && (inspection.PreventiveMaintenanceFormId is null
                || inspection.PreventiveMaintenanceForm?.Status
                    == PreventiveMaintenanceFormStatusCatalog.Acknowledged);
    }

    private static bool TryGetActorId(ClaimsPrincipal principal, out Guid actorId) =>
        Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}
