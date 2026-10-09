using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;
using UniPM.Api.Features.Auth;
using UniPM.Api.Features.Schedules;

namespace UniPM.Api.Features.Assets;

public static class AssetsEndpoints
{
    public static IEndpointRouteBuilder MapAssetsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/assets").WithTags("Assets");

        group.MapPost("/", async (
            CreateAssetDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            PreventiveMaintenanceScheduleGenerationService scheduleGenerator,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = dto.Validate();
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            var assetCode = AssetCodeValue.Normalize(dto.AssetCode);
            AssetCategoryCatalog.TryNormalize(dto.AssetCategory, out var assetCategory);

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            }

            await using var transactionScope = transaction;
            var duplicateAssetCode = await context.Assets
                .AnyAsync(asset => asset.AssetCode == assetCode, cancellationToken);

            if (duplicateAssetCode)
            {
                return ApiErrors.Conflict($"Asset code '{assetCode}' already exists.");
            }

            var now = timeProvider.GetUtcNow();
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = assetCode,
                AssetCategory = assetCategory,
                Building = NormalizeOptional(dto.Building),
                Department = NormalizeOptional(dto.Department),
                Location = NormalizeOptional(dto.Location),
                VerificationLatitude = dto.VerificationLatitude,
                VerificationLongitude = dto.VerificationLongitude,
                VerificationRadiusMeters = dto.VerificationRadiusMeters,
                Status = AssetStatusCatalog.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            asset.QrCodeValue = AssetQrCodeValue.Create(asset.AssetCategory, asset.Id);

            context.Assets.Add(asset);
            try
            {
                await scheduleGenerator.AddCurrentYearUpcomingCyclesAsync(
                    context,
                    asset,
                    now,
                    cancellationToken,
                    transaction);
                await context.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateException exception)
                when (DatabaseConstraintViolation.IsUniqueConstraint(
                    exception,
                    PreventiveMaintenanceScheduleGenerationService.UniqueIndexName))
            {
                return ApiErrors.Conflict("A preventive-maintenance cycle already exists for this asset.");
            }
            catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
            {
                return ApiErrors.Conflict("Asset code or QR code value already exists.");
            }
            catch (ScheduleBatchMutationConflictException)
            {
                return ApiErrors.Conflict("The current PM batch changed while the asset was being enrolled. Retry the registration.");
            }

            return Results.Created(
                $"/api/v1/assets/{asset.Id}",
                AssetResponse.FromAsset(asset));
        })
        .WithName("CreateAsset")
        .WithSummary("Creates an asset in the current UniPM study scope")
        .Produces<AssetResponse>(StatusCodes.Status201Created)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status409Conflict)
        .RequireAuthorization(AuthPolicyCatalog.CanManageAssets);

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);
            return asset is not null ? Results.Ok(AssetResponse.FromAsset(asset)) : ApiErrors.NotFound("Asset not found.");
        })
        .WithName("GetAsset")
        .WithSummary("Gets an asset by its identifier")
        .Produces<AssetResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/verification-location", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            return asset is not null
                ? Results.Ok(AssetVerificationLocationResponse.FromAsset(asset))
                : ApiErrors.NotFound("Asset not found.");
        })
        .WithName("GetAssetVerificationLocation")
        .WithSummary("Gets the verification location configuration for an asset")
        .Produces<AssetVerificationLocationResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .RequireAuthorization(AuthPolicyCatalog.CanManageAssets);

        group.MapPut("/{id}/verification-location", async (
            Guid id,
            UpdateAssetVerificationLocationDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = dto.Validate();
            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
            if (asset is null)
            {
                return ApiErrors.NotFound("Asset not found.");
            }

            asset.VerificationLatitude = dto.VerificationLatitude;
            asset.VerificationLongitude = dto.VerificationLongitude;
            asset.VerificationRadiusMeters = dto.VerificationRadiusMeters;
            asset.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(AssetVerificationLocationResponse.FromAsset(asset));
        })
        .WithName("UpdateAssetVerificationLocation")
        .WithSummary("Replaces or clears an asset verification location")
        .Produces<AssetVerificationLocationResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound)
        .RequireAuthorization(AuthPolicyCatalog.CanManageAssets);

        group.MapGet("/", async (
            string? assetCategory,
            string? status,
            string? building,
            string? department,
            string? search,
            int? limit,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            var normalizedDepartment = string.IsNullOrWhiteSpace(department)
                ? null
                : department.Trim();
            var normalizedSearch = string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim();
            var validationErrors = new Dictionary<string, string[]>();
            if (normalizedDepartment?.Length > 256)
            {
                validationErrors[nameof(department)] = ["Department must be 256 characters or fewer."];
            }

            if (normalizedSearch?.Length > 256)
            {
                validationErrors[nameof(search)] = ["Search must be 256 characters or fewer."];
            }

            if (validationErrors.Count > 0)
            {
                return ApiErrors.Validation(validationErrors);
            }

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var query = context.Assets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(assetCategory))
            {
                if (!AssetCategoryCatalog.TryNormalize(assetCategory, out var normalizedCategory))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(assetCategory)] = ["Asset category must be one of the selected UniPM study scope categories."]
                    });
                }

                query = query.Where(asset => asset.AssetCategory == normalizedCategory);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!AssetStatusCatalog.TryNormalize(status, out var normalizedStatus))
                {
                    return ApiErrors.Validation(new Dictionary<string, string[]>
                    {
                        [nameof(status)] = ["Status must be a supported asset status."]
                    });
                }

                query = query.Where(asset => asset.Status == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(building))
            {
                var normalizedBuilding = building.Trim().ToUpper();
                query = query.Where(asset => asset.Building != null && asset.Building.ToUpper() == normalizedBuilding);
            }

            if (normalizedDepartment is not null)
            {
                var departmentKey = normalizedDepartment.ToUpperInvariant();
                query = query.Where(asset => asset.Department != null && asset.Department.ToUpper() == departmentKey);
            }

            if (normalizedSearch is not null)
            {
                var searchKey = normalizedSearch.ToUpperInvariant();
                query = query.Where(asset =>
                    asset.AssetCode.Contains(searchKey) ||
                    (asset.QrCodeValue != null && asset.QrCodeValue.ToUpper().Contains(searchKey)) ||
                    (asset.Building != null && asset.Building.ToUpper().Contains(searchKey)) ||
                    (asset.Department != null && asset.Department.ToUpper().Contains(searchKey)) ||
                    (asset.Location != null && asset.Location.ToUpper().Contains(searchKey)));
            }

            var orderedQuery = query.OrderBy(asset => asset.AssetCode);
            var maxResults = limit.HasValue ? Math.Clamp(limit.Value, 1, 100) : (int?)null;
            var finalQuery = maxResults.HasValue ? orderedQuery.Take(maxResults.Value) : orderedQuery;

            var assets = await finalQuery
                .Select(asset => new AssetResponse(
                    asset.Id,
                    asset.AssetCode,
                    asset.AssetCategory,
                    asset.Building,
                    asset.Department,
                    asset.Location,
                    asset.QrCodeValue,
                    asset.Status,
                    asset.CreatedAt,
                    asset.UpdatedAt,
                    asset.VerificationLatitude != null
                        && asset.VerificationLongitude != null
                        && asset.VerificationRadiusMeters != null))
                .ToListAsync(cancellationToken);

            return Results.Ok(assets);
        })
        .WithName("ListAssets")
        .WithSummary("Lists assets using supported category, status, building, department, and search filters")
        .Produces<IReadOnlyList<AssetResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/by-code/{assetCode}", async (
            string assetCode,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(assetCode)] = ["Asset code is required."]
                });
            }

            var normalizedAssetCode = AssetCodeValue.Normalize(assetCode);

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    asset => asset.AssetCode == normalizedAssetCode,
                    cancellationToken);

            return asset is not null ? Results.Ok(AssetResponse.FromAsset(asset)) : ApiErrors.NotFound("Asset not found.");
        })
        .WithName("GetAssetByCode")
        .WithSummary("Gets an asset by its canonical asset code")
        .Produces<AssetResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/by-qr/{qrCodeValue}", async (
            string qrCodeValue,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(qrCodeValue))
            {
                return ApiErrors.Validation(new Dictionary<string, string[]>
                {
                    [nameof(qrCodeValue)] = ["QR code value is required."]
                });
            }

            var normalizedQrCodeValue = AssetCodeValue.NormalizeQrCode(qrCodeValue);

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    asset => asset.QrCodeValue == normalizedQrCodeValue,
                    cancellationToken);

            return asset is not null ? Results.Ok(AssetResponse.FromAsset(asset)) : ApiErrors.NotFound("Asset not found.");
        })
        .WithName("GetAssetByQr")
        .WithSummary("Gets an asset by its QR identifier")
        .Produces<AssetResponse>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<Microsoft.AspNetCore.Mvc.ProblemDetails>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record AssetResponse(
    Guid Id,
    string AssetCode,
    string AssetCategory,
    string? Building,
    string? Department,
    string? Location,
    string? QrCodeValue,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasVerificationLocation)
{
    internal static AssetResponse FromAsset(Asset asset)
    {
        return new AssetResponse(
            asset.Id,
            asset.AssetCode,
            asset.AssetCategory,
            asset.Building,
            asset.Department,
            asset.Location,
            asset.QrCodeValue,
            asset.Status,
            asset.CreatedAt,
            asset.UpdatedAt,
            asset.VerificationLatitude.HasValue
                && asset.VerificationLongitude.HasValue
                && asset.VerificationRadiusMeters.HasValue);
    }
}

public sealed record AssetVerificationLocationResponse(
    double? VerificationLatitude,
    double? VerificationLongitude,
    double? VerificationRadiusMeters)
{
    internal static AssetVerificationLocationResponse FromAsset(Asset asset)
        => new(
            asset.VerificationLatitude,
            asset.VerificationLongitude,
            asset.VerificationRadiusMeters);
}

public class CreateAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public double? VerificationLatitude { get; set; }
    public double? VerificationLongitude { get; set; }
    public double? VerificationRadiusMeters { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(AssetCode))
        {
            errors.Add(nameof(AssetCode), ["Asset code is required."]);
        }
        else if (AssetCodeValue.Normalize(AssetCode).Length > AssetCodeValue.MaxLength)
        {
            errors.Add(nameof(AssetCode), [$"Asset code must not exceed {AssetCodeValue.MaxLength} characters."]);
        }

        if (string.IsNullOrWhiteSpace(AssetCategory))
        {
            errors.Add(nameof(AssetCategory), ["Asset category is required."]);
        }
        else if (!AssetCategoryCatalog.TryNormalize(AssetCategory, out _))
        {
            errors.Add(
                nameof(AssetCategory),
                ["Asset category must be one of the selected UniPM study scope categories."]);
        }

        if (string.IsNullOrWhiteSpace(Department))
        {
            errors.Add(nameof(Department), ["Department is required for preventive maintenance assets."]);
        }

        ValidateOptionalLength(Building, nameof(Building), AssetCodeValue.MetadataMaxLength, errors);
        ValidateOptionalLength(Department, nameof(Department), AssetCodeValue.MetadataMaxLength, errors);
        ValidateOptionalLength(Location, nameof(Location), AssetCodeValue.MetadataMaxLength, errors);
        AssetVerificationLocationRules.AddConfigurationErrors(
            VerificationLatitude,
            VerificationLongitude,
            VerificationRadiusMeters,
            errors);

        return errors;
    }

    private static void ValidateOptionalLength(
        string? value,
        string fieldName,
        int maxLength,
        Dictionary<string, string[]> errors)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[fieldName] = [$"{fieldName} must not exceed {maxLength} characters."];
        }
    }
}

public sealed class UpdateAssetVerificationLocationDto
{
    public double? VerificationLatitude { get; set; }
    public double? VerificationLongitude { get; set; }
    public double? VerificationRadiusMeters { get; set; }

    internal Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        AssetVerificationLocationRules.AddConfigurationErrors(
            VerificationLatitude,
            VerificationLongitude,
            VerificationRadiusMeters,
            errors);
        return errors;
    }
}
