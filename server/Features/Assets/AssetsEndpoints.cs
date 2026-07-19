using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;
using UniPM.Api.Features.Auth;

namespace UniPM.Api.Features.Assets;

public static class AssetsEndpoints
{
    public static IEndpointRouteBuilder MapAssetsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/assets").WithTags("Assets");

        group.MapPost("/", async (
            CreateAssetDto dto,
            IDbContextFactory<ApplicationDbContext> factory,
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
            var duplicateAssetCode = await context.Assets
                .AnyAsync(asset => asset.AssetCode == assetCode, cancellationToken);

            if (duplicateAssetCode)
            {
                return ApiErrors.Conflict($"Asset code '{assetCode}' already exists.");
            }

            var now = DateTimeOffset.UtcNow;
            var asset = new Asset
            {
                Id = Guid.NewGuid(),
                AssetCode = assetCode,
                AssetCategory = assetCategory,
                Building = NormalizeOptional(dto.Building),
                Department = NormalizeOptional(dto.Department),
                Location = NormalizeOptional(dto.Location),
                Status = AssetStatusCatalog.Active,
                CreatedAt = now,
                UpdatedAt = now
            };

            asset.QrCodeValue = AssetQrCodeValue.Create(asset.AssetCategory, asset.Id);

            context.Assets.Add(asset);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (DatabaseConstraintViolation.IsUniqueConstraint(exception))
            {
                return ApiErrors.Conflict("Asset code or QR code value already exists.");
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

        group.MapGet("/", async (
            string? assetCategory,
            string? status,
            string? building,
            string? department,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
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

            if (!string.IsNullOrWhiteSpace(department))
            {
                var normalizedDepartment = department.Trim().ToUpper();
                query = query.Where(asset => asset.Department != null && asset.Department.ToUpper() == normalizedDepartment);
            }

            var assets = await query
                .OrderBy(asset => asset.AssetCode)
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
                    asset.UpdatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(assets);
        })
        .WithName("ListAssets")
        .WithSummary("Lists assets using supported category, status, building, and department filters")
        .Produces<IReadOnlyList<AssetResponse>>(StatusCodes.Status200OK)
        .Produces<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(StatusCodes.Status400BadRequest);

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
    DateTimeOffset UpdatedAt)
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
            asset.UpdatedAt);
    }
}

public class CreateAssetDto
{
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }

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

        ValidateOptionalLength(Building, nameof(Building), AssetCodeValue.MetadataMaxLength, errors);
        ValidateOptionalLength(Department, nameof(Department), AssetCodeValue.MetadataMaxLength, errors);
        ValidateOptionalLength(Location, nameof(Location), AssetCodeValue.MetadataMaxLength, errors);

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
