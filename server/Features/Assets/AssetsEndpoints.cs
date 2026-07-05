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
            return asset is not null ? Results.Ok(asset) : ApiErrors.NotFound("Asset not found.");
        });

        return endpoints;
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
