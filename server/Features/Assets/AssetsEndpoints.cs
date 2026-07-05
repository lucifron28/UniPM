using Microsoft.EntityFrameworkCore;
using UniPM.Api.Data;
using UniPM.Api.Features;
using UniPM.Api.Features.ReferenceData;
using UniPM.Api.Models;

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

            var assetCode = dto.AssetCode.Trim();
            var assetCategory = dto.AssetCategory.Trim().ToLowerInvariant();

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var duplicateAssetCode = await context.Assets
                .AnyAsync(asset => asset.AssetCode.ToUpper() == assetCode.ToUpper(), cancellationToken);

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
                Building = dto.Building?.Trim(),
                Department = dto.Department?.Trim(),
                Location = dto.Location?.Trim(),
                Status = "Active",
                CreatedAt = now,
                UpdatedAt = now
            };

            asset.QrCodeValue = $"UNIPM-{asset.AssetCategory.ToUpper().Replace(" ", "")}-{asset.Id.ToString().Substring(0, 8)}";

            context.Assets.Add(asset);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/v1/assets/{asset.Id}", asset);
        });

        group.MapGet("/{id}", async (
            Guid id,
            IDbContextFactory<ApplicationDbContext> factory,
            CancellationToken cancellationToken) =>
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets.FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);
            return asset is not null ? Results.Ok(AssetResponse.FromAsset(asset)) : ApiErrors.NotFound("Asset not found.");
        });

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
                var normalizedCategory = assetCategory.Trim().ToUpper();
                query = query.Where(asset => asset.AssetCategory.ToUpper() == normalizedCategory);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpper();
                query = query.Where(asset => asset.Status.ToUpper() == normalizedStatus);
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
        });

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

            var normalizedQrCodeValue = qrCodeValue.Trim().ToUpper();

            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var asset = await context.Assets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    asset => asset.QrCodeValue != null && asset.QrCodeValue.ToUpper() == normalizedQrCodeValue,
                    cancellationToken);

            return asset is not null ? Results.Ok(AssetResponse.FromAsset(asset)) : ApiErrors.NotFound("Asset not found.");
        });

        return endpoints;
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

        if (string.IsNullOrWhiteSpace(AssetCategory))
        {
            errors.Add(nameof(AssetCategory), ["Asset category is required."]);
        }
        else if (!AssetCategoryCatalog.ContainsCode(AssetCategory))
        {
            errors.Add(
                nameof(AssetCategory),
                ["Asset category must be one of the selected UniPM study scope categories."]);
        }

        return errors;
    }
}
